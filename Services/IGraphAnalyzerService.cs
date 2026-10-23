using CircuitMatrices.Models;

namespace CircuitMatrices.Services
{
    /// <summary>Contract of the analysis engine (registered in Program.cs and injected into the controller).</summary>
    public interface IGraphAnalyzerService
    {
        AnalyzeResponse Analyze(AnalyzeRequest request);
    }
}
