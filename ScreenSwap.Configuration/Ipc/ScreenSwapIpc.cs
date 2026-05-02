using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenSwap.Configuration.Ipc;

public static partial class ScreenSwapIpc
{
    public const string PipeName = "ScreenSwap.Agent";
    private const int ConnectTimeoutMs = 300;

    public static async Task<bool> SendCommandAsync(ScreenSwapIpcCommand command, CancellationToken cancellationToken = default)
    {
        if (!IsAgentPipeAvailable())
            return false;

        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(ConnectTimeoutMs, cancellationToken).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(command.ToString());
            await client.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await client.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException) { return false; }
        catch (IOException) { return false; }
        catch (OperationCanceledException) { return false; }
    }

    private static bool IsAgentPipeAvailable()
        => WaitNamedPipe($@"\\.\pipe\{PipeName}", ConnectTimeoutMs);

    [LibraryImport("kernel32.dll", EntryPoint = "WaitNamedPipeW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WaitNamedPipe(string name, int timeout);
}
