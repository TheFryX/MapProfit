using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MapProfit.Core;

public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static ProfitSession Load(string path)
    {
        var data = JsonSerializer.Deserialize<ProfitSession>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Empty session file.");
        if (data.Version != 1 || data.Runs == null || data.Runs.Any(r => r.Loot == null || r.Areas == null))
            throw new InvalidDataException("Unsupported session data.");
        foreach (var run in data.Runs.Where(r => r.Id != data.CurrentRunId && !r.RateProfitChaos.HasValue))
            run.CheckpointRate();
        return data;
    }

    public static void Save(string path, ProfitSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(session, JsonOptions), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
        else File.Move(temp, path);
    }

    public static void ExportCsv(string path, ProfitSession session)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("RunId,Map,Started,ActiveSeconds,EntryCostChaos,ProfitChaos,Item,Quantity,Chaos,UnpricedQuantity");
        foreach (var run in session.Runs)
        {
            var prefix = $"{run.Id},{Cell(run.Name)},{run.Started:O},{Number(run.ActiveSeconds)},{Number(run.EntryCostChaos)},{Number(run.ProfitChaos)}";
            foreach (var entry in run.Loot.Values.Where(e => e.Quantity != 0))
                writer.WriteLine($"{prefix},{Cell(entry.Name)},{entry.Quantity},{Number(entry.Chaos)},{entry.UnpricedQuantity}");
            if (!run.Loot.Values.Any(e => e.Quantity != 0)) writer.WriteLine(prefix + ",,0,0,0");
        }
    }
    private static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    private static string Cell(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
