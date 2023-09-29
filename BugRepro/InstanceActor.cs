using Akka.Actor;
using Akka.Event;
using Akka.IO;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using Tcp = Akka.Streams.Dsl.Tcp;

namespace BugRepro;

public class InstanceActor : UntypedActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private SocketState _state = Context.System.CreateSocketState();

    protected override void OnReceive(object message)
    {
        _log.Info($"received: {message}");
        _state = MessageHandler(message, _state);
    }

    private SocketState MessageHandler(object message, SocketState state)
    {
        switch (message)
        {
            case Tcp.OutgoingConnection c:
                _log.Info($"connected to {c.RemoteAddress}");
                return state;
            case ICommand c:
                switch (c)
                {
                    case ConnectTo ct:
                        var ep = ct.EndPoint;
                        var killSwitch = Flow.FromSinkAndSource(state.Sink, state.Source.Run(Context.System.Materializer()))
                            .Recover(ex =>
                            {
                                _log.Error(ex, $"failed: {ex.Message}");
                                return Option<ByteString>.None;
                            })
                            .ViaMaterialized(KillSwitches.Single<ByteString>(), Keep.Right)
                            .Join(
                                Context.System.TcpStream().OutgoingConnection(ep)
                                    .Recover(ex =>
                                    {
                                        _log.Error(ex, $"failed: {ex.Message}");
                                        return Option<ByteString>.None;
                                    }))
                            .Run(Context.System.Materializer());
                        return state with { KillSwitch = killSwitch };
                    
                    case StopConnections:
                        state.KillSwitch?.Shutdown();
                        return state with { KillSwitch = null };
                    
                    default:
                        throw new IndexOutOfRangeException(c.GetType().ToString());
                }
            default:
                _log.Warning("unhandled message {0}", message);
                Unhandled(message);
                return state;
        }
    }
}