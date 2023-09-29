// //-----------------------------------------------------------------------
// // <copyright file="SocketState.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System.Net;
using Akka;
using Akka.IO;
using Akka.Streams;
using Akka.Streams.Dsl;

namespace BugRepro;

public interface ICommand { }

public record ConnectTo(IPEndPoint EndPoint) : ICommand;

public record StopConnections : ICommand;

public record struct SocketState(
    UniqueKillSwitch? KillSwitch, 
    Sink<ByteString, NotUsed> Sink,
    IRunnableGraph<Source<ByteString, NotUsed>> Source);