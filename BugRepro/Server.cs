//-----------------------------------------------------------------------
// <copyright file="Server.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Net;
using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace BugRepro;

public static class Server
{
    public static string ToActorName(this EndPoint ep)
        => ep is IPEndPoint ie 
            ? $"{ie.Address}:{ie.Port}" 
            : throw new Exception($"{nameof(EndPoint)} is not an instance of {nameof(IPEndPoint)}");

    public static IActorRef StartListener(this ActorSystem system, string address, int port)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(address), port);
        return system.ActorOf(Props.Create(() => new ServerListenerActor(endpoint)), $"test-echo-server-{address}-{port}");
    }

    public static SocketState CreateSocketState(this ActorSystem system)
    {
        var materializer = system.Materializer();
        var (rxSink, rxSource) = MergeHub.Source<ByteString>()
            .ToMaterialized(BroadcastHub.Sink<ByteString>(), Keep.Both)
            .Run(materializer);

        var (r1, r2) = Source.ActorRef<string>(10, OverflowStrategy.Fail)
            .Recover(ex =>
            {
                system.Log.Error(ex, $"failed: {ex.Message}");
                return Option<string>.None;
            })
            .Log("tx")
            .WithAttributes(new Attributes(new Attributes.LogLevels(LogLevel.InfoLevel, LogLevel.InfoLevel, LogLevel.ErrorLevel)))
            .Select(x => ByteString.FromString($"{x}\n"))
            .PreMaterialize(materializer);

        var (s1, s2) = (r1, r2.ToMaterialized(BroadcastHub.Sink<ByteString>(), Keep.Right));

        rxSource
            .Recover<ByteString, NotUsed>(ex =>
            {
                system.Log.Error(ex, $"failed: {ex.Message}");
                return Option<ByteString>.None;
            })
            .Select(bs => bs.ToString()?.Trim())
            .Log("rx")
            .WithAttributes(new Attributes(new Attributes.LogLevels(LogLevel.InfoLevel, LogLevel.InfoLevel, LogLevel.ErrorLevel)))
            .Select(_ =>
                ThreadLocalRandom.Current.Next(0, 255) switch
                {
                    //< 10 => "stop",
                    //< 20 => "abort",
                    var i => i.ToString()
                })
            .To(Sink.ActorRef<string>(s1, "stop\n"))
            .Run(materializer);

        return new SocketState(null, rxSink, s2);
    }
    
}

