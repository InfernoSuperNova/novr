using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;

const string PayloadMarker = "\n__NOVR_SFX_PAYLOAD_v1__\n";

var executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
var payload = ReadPayload(executablePath);
var extractionDirectory = Path.Combine(Path.GetTempPath(), "NOVR.Installer." + Guid.NewGuid().ToString("N"));

try
{
    Directory.CreateDirectory(extractionDirectory);

    using (var payloadStream = new MemoryStream(payload, writable: false))
    using (var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read))
    {
        archive.ExtractToDirectory(extractionDirectory, overwriteFiles: true);
    }

    var installerPath = Path.Combine(extractionDirectory, "NOVR.Installer.exe");
    if (!File.Exists(installerPath))
    {
        throw new FileNotFoundException("The embedded installer payload did not contain NOVR.Installer.exe.", installerPath);
    }

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = installerPath,
        Arguments = string.Join(" ", args.Select(QuoteArgument)),
        UseShellExecute = false,
        WorkingDirectory = extractionDirectory
    });

    process?.WaitForExit();
    return process?.ExitCode ?? 1;
}
finally
{
    TryDeleteDirectory(extractionDirectory);
}

static byte[] ReadPayload(string executablePath)
{
    var markerBytes = Encoding.ASCII.GetBytes(PayloadMarker);
    var bytes = File.ReadAllBytes(executablePath);
    var markerOffset = bytes.AsSpan().LastIndexOf(markerBytes);

    if (markerOffset < 0)
        throw new InvalidOperationException("This executable does not contain an embedded NOVR installer payload.");

    var payloadOffset = markerOffset + markerBytes.Length;
    return bytes[payloadOffset..];
}

static string QuoteArgument(string argument)
{
    return argument.Contains(' ') || argument.Contains('"')
        ? "\"" + argument.Replace("\"", "\\\"") + "\""
        : argument;
}

static void TryDeleteDirectory(string path)
{
    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch
    {
        // Best effort cleanup. Leaving the extracted installer in temp is preferable to masking install failures.
    }
}
