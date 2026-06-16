using System.Diagnostics;
using System.Text;

namespace Forge.Db;

/// <summary>Thin wrapper for running an external CLI (pg-schema-diff) and capturing its output.</summary>
public static class ProcessRunner
{
    public sealed record Result(int ExitCode, string StdOut, string StdErr)
    {
        public bool Ok => ExitCode == 0;
    }

    public static Result Run(string fileName, IEnumerable<string> args, string? workingDir = null,
        IDictionary<string, string>? extraEnv = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDir ?? Directory.GetCurrentDirectory(),
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (extraEnv is not null)
            foreach (var (k, v) in extraEnv) psi.Environment[k] = v;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            return new Result(127, "", $"failed to launch '{fileName}': {ex.Message}");
        }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return new Result(p.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
