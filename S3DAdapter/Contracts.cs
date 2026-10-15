using PaintRules;

namespace S3DAdapter;

// OWNER-TBD: S3D version/.NET Framework, property interface names, CodeList values and units.
public interface IS3DReader
{
    IReadOnlyList<PipeRunData> ReadPipeRuns();
    IReadOnlyList<string> ExportDistinctSpecs();
}

public interface IS3DWriter
{
    // OWNER-TBD: Resolve Paint Code target property and validate CodeList before any transaction.
    // Implementation must commit as one batch or abort, then mark failed writes ERROR.
    void WriteApproved(IReadOnlyList<EvaluationResult> approved);
}
