// //-----------------------------------------------------------------------
// // <copyright file="ServerListenerActor.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System.Net;
using Akka.Actor;
using Akka.Event;
using Akka.IO;

namespace BugRepro;

public class ServerListenerActor : UntypedActor
{
    private readonly ILoggingAdapter _log;

    public ServerListenerActor(EndPoint endpoint)
    {
        _log = Context.GetLogger();
        var tcpManager = Tcp.Manager(Context.System);
        tcpManager.Tell(TcpMessage.Bind(Self, endpoint, 100));
    }

    protected override void OnReceive(object message)
    {
        _log.Info($"[MSG][{message.ToString()?.Trim()}]");
            
        switch (message)
        {
            case Tcp.Connected msg:
                var handler = Context.System.ActorOf(
                    Props.Create(() => new ListenerHandlerActor(Sender)), msg.RemoteAddress.ToActorName());
                Context.Watch(handler);
                Sender.Tell(TcpMessage.Register(handler));
                break;
            case Terminated t:
                _log.Info("Actor terminated: {0}", t.ActorRef.Path);
                break;
            default:
                Unhandled(message);
                break;
        }
    }
    
    private class ListenerHandlerActor: UntypedActor
    {
        private readonly IActorRef _connection;
        private readonly ILoggingAdapter _log;
        
        public ListenerHandlerActor(IActorRef connection)
        {
            _connection = connection;
            _log = Context.GetLogger();
        }

        protected override void PreStart()
        {
            base.PreStart();
            _connection.Tell(TcpMessage.Write(ByteString.FromString("Hello!\n")));
        }

        protected override void OnReceive(object message)
        {
            _log.Info($"[MSG][{message.ToString()?.Trim()}]");

            switch (message)
            {
                case Tcp.Received msg:
                    var data = msg.Data;
                    var rx = data.ToString();
                    _log.Info($"[SERVER_RX][{rx?.Trim()}]");
                    switch (rx)
                    {
                        case "stop\n":
                            _connection.Tell(TcpMessage.Close());
                            break;
                        case "abort\n":
                            _connection.Tell(TcpMessage.Abort());
                            break;
                        default:
                            Task.Run(async () =>
                            {
                                await Task.Delay(500);
                                _connection.Tell(TcpMessage.Write(data));
                            }).PipeTo(Self);
                            break;
                    }
                    break;
                case Terminated:
                case Tcp.ConnectionClosed:
                    Context.Stop(Self);
                    break;
                default:
                    Unhandled(message);
                    break;
            }
        }
    }
}