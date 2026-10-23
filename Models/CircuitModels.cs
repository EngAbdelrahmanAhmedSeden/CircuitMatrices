using System.Collections.Generic;

namespace CircuitMatrices.Models
{
    // ------------------------------------------------------------------
    // Request
    // ------------------------------------------------------------------

    /// <summary>One branch of the network: V = Z (J + I) - E (lecture 3, equation 1).</summary>
    public class BranchInput
    {
        public string Name { get; set; }
        public int From { get; set; }     // node label (1-based), circuit mode only
        public int To { get; set; }       // node label (1-based), circuit mode only
        public double Z { get; set; }     // branch impedance (resistance)
        public double E { get; set; }     // branch voltage source
        public double I { get; set; }     // branch current source
    }

    public class AnalyzeRequest
    {
        /// <summary>circuit | incidence | tieset | cutset</summary>
        public string Mode { get; set; }
        public List<BranchInput> Branches { get; set; }
        /// <summary>Matrix for the incidence / tieset / cutset modes (rows of numbers).</summary>
        public double[][] Matrix { get; set; }
        /// <summary>Optional: names of the branches that must form the tree.</summary>
        public List<string> Tree { get; set; }
        /// <summary>Optional reference node (1-based). Defaults to the last node.</summary>
        public int? Reference { get; set; }
    }

    // ------------------------------------------------------------------
    // Response
    // ------------------------------------------------------------------

    public class NamedMatrix
    {
        public string Label { get; set; }
        public List<string> RowLabels { get; set; }
        public List<string> ColLabels { get; set; }
        public double[][] Data { get; set; }
    }

    public class Step
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public List<NamedMatrix> Matrices { get; set; } = new List<NamedMatrix>();
    }

    public class GraphEdge
    {
        public string Name { get; set; }
        public int From { get; set; }     // 1-based node label
        public int To { get; set; }       // 1-based node label
        public bool IsTree { get; set; }
    }

    public class MethodResult
    {
        public bool Ok { get; set; }
        public string Error { get; set; }
        public string Note { get; set; }
        public double[] Unknowns { get; set; }   // loop currents I_L or node-pair voltages V_T
        public double[] J { get; set; }          // branch currents
        public double[] V { get; set; }          // branch voltages
    }

    public class BranchResult
    {
        public string Name { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public bool IsTree { get; set; }
        public double Z { get; set; }
        public double E { get; set; }
        public double I { get; set; }
        public double? LoopJ { get; set; }
        public double? LoopV { get; set; }
        public double? CutJ { get; set; }
        public double? CutV { get; set; }
    }

    public class CheckItem
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public bool Passed { get; set; }
    }

    public class AnalyzeResponse
    {
        public string Error { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();

        public bool HasTopology { get; set; }       // false when only a B or C matrix was supplied
        public int Nodes { get; set; }
        public int Branches { get; set; }
        public int Links { get; set; }
        public int Twigs { get; set; }
        public bool Connected { get; set; }
        public int ReferenceNode { get; set; }

        public int? TreeCount { get; set; }
        public List<string> Trees { get; set; } = new List<string>();
        public bool TreesTruncated { get; set; }
        public bool TreesSkipped { get; set; }

        public List<string> TreeBranches { get; set; } = new List<string>();
        public List<string> LinkBranches { get; set; } = new List<string>();
        public List<GraphEdge> Edges { get; set; } = new List<GraphEdge>();

        public NamedMatrix A { get; set; }          // full incidence matrix
        public NamedMatrix Reduced { get; set; }    // reduced incidence matrix
        public NamedMatrix B { get; set; }          // tie-set matrix, columns = [twigs | links]
        public NamedMatrix C { get; set; }          // cut-set matrix, columns = [twigs | links]

        public MethodResult Loop { get; set; }
        public MethodResult CutSet { get; set; }
        public List<BranchResult> Results { get; set; } = new List<BranchResult>();
        public List<CheckItem> Checks { get; set; } = new List<CheckItem>();
        public List<Step> Steps { get; set; } = new List<Step>();
    }
}

namespace CircuitMatrices.Models
{
    /// <summary>A ready-made input shown in the "Load example" list of the Analyzer view.</summary>
    public class ExamplePreset
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Mode { get; set; }            // circuit | incidence | tieset | cutset
        public string Tree { get; set; }
        public int? Reference { get; set; }
        public string Matrix { get; set; }          // matrix modes
        public List<string> Names { get; set; }     // matrix modes: branch names
        public List<BranchInput> Branches { get; set; }   // circuit mode
    }

    /// <summary>Model of the Home/Index view.</summary>
    public class HomeViewModel
    {
        public List<ExamplePreset> Examples { get; set; } = new List<ExamplePreset>();
    }
}
