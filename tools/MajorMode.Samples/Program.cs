using System.Diagnostics;
using MajorMode.Core;
using MajorMode.Core.Model;
using MajorMode.Core.Parsing;
using MajorMode.Rendering;
using MajorMode.Rendering.Export;

// Генерация образцов картинок для ручного просмотра.
// Использование: dotnet run --project tools/MajorMode.Samples -- [папка] [--all-formats] [--4k]
var outDir = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? Path.Combine("artifacts", "samples");
var allFormats = args.Contains("--all-formats");
var include4K = args.Contains("--4k");
Directory.CreateDirectory(outDir);

var chords = new (string Id, ChordInput Input)[]
{
    ("C", ChordInput.Create("C", "6", new FingerInput("1", "2"), new FingerInput("2", "4"), new FingerInput("3", "5"))),
    ("F", ChordInput.Create("F", "", new FingerInput("1", "B"), new FingerInput("2", "3"), new FingerInput("3", "5"), new FingerInput("3", "4"))),
    ("Barre5", ChordInput.Create("Am", "", new FingerInput("5", "B"), new FingerInput("7", "5"), new FingerInput("7", "4"), new FingerInput("6", "3"))),
    ("Fsharp_m7", ChordInput.Create("F#m7", "XX", new FingerInput("2", "B"), new FingerInput("4", "5"))),
};

var canvases = include4K
    ? Enum.GetValues<CanvasFormat>()
    : [CanvasFormat.Landscape16x9, CanvasFormat.Portrait9x16];
var formats = allFormats ? Enum.GetValues<ExportFormat>() : [ExportFormat.Png];

foreach (var (id, input) in chords)
{
    var analysis = ChordAnalyzer.Analyze(input);
    if (!analysis.CanRender)
    {
        Console.Error.WriteLine($"{id}: {ChordAnalyzer.BuildErrorSummary(analysis.Messages)}");
        continue;
    }

    foreach (var style in Enum.GetValues<ImageStyle>())
    {
        foreach (var orientation in Enum.GetValues<FretboardOrientation>())
        {
            foreach (var canvas in canvases)
            {
                var sw = Stopwatch.StartNew();
                using var image = ChordRenderer.Render(analysis.Chord!, analysis.Range!.Value, new RenderOptions(orientation, canvas, style));
                var renderMs = sw.ElapsedMilliseconds;
                foreach (var format in formats)
                {
                    sw.Restart();
                    var bytes = ImageExporter.Encode(image, format, style);
                    var name = $"{id}_{style}_{orientation}_{canvas}{format.FileExtension()}";
                    File.WriteAllBytes(Path.Combine(outDir, name), bytes);
                    Console.WriteLine($"{name}: {bytes.Length / 1024} КБ, рендер {renderMs} мс, кодирование {sw.ElapsedMilliseconds} мс");
                }
            }
        }
    }
}
