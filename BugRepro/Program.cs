using System.Net;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;

namespace BugRepro;

public static class Program
{
    private static readonly Config Config = ConfigurationFactory.ParseString(
        """
            akka {
                loglevel = INFO
                stdout-loglevel = INFO
                actor {
                    debug {
                        receive = on
                        autoreceive = on
                        lifecycle = on
                        event-stream = on
                        unhandled = on
                    }
            }
        }
        """);

    private static readonly ActorSystem ActorSystem;
    private static readonly ILoggingAdapter Log;

    static Program()
    {
        ActorSystem = ActorSystem.Create("my-system", Config);
        Log = Logging.GetLogger(ActorSystem, nameof(Program));
    }
        
    public static async Task Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            Log.Info("Ctrl+C pressed - exiting...");
            eventArgs.Cancel = true;
            cts.Cancel(false);
        };
        
        Log.Info("Press Ctrl+C to exit...");

        var token = cts.Token;
        var listener = ActorSystem.StartListener("127.0.0.1", 8888);
        var instance = ActorSystem.ActorOf(Props.Create(() => new InstanceActor()));

        do
        {
            try
            {
                instance.Tell(new ConnectTo(new IPEndPoint(IPAddress.Loopback, 8888)));
                await Task.Delay(5000, cts.Token);
                instance.Tell(new StopConnections());
                await Task.Delay(1000, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
        } while (!token.IsCancellationRequested);

        await ActorSystem.Terminate();
    }
}