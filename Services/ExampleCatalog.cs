using System.Collections.Generic;
using CircuitMatrices.Models;

namespace CircuitMatrices.Services
{
    /// <summary>The worked examples from the CAD lectures (plus a bridge circuit) offered on the Analyzer page.</summary>
    public static class ExampleCatalog
    {
        public static List<ExamplePreset> All()
        {
            return new List<ExamplePreset>
            {
                // Lecture 3: 10 V source with 5 ohm in series, 5 ohm to ground, 10 ohm, 5 ohm. Ground = node 3.
                new ExamplePreset
                {
                    Key = "lec3",
                    Title = "Lecture 3 - 10 V network (find J and V)",
                    Mode = "circuit",
                    Tree = "",
                    Reference = 3,
                    Branches = new List<BranchInput>
                    {
                        new BranchInput { Name = "a", From = 3, To = 1, Z = 5,  E = 10 },
                        new BranchInput { Name = "b", From = 1, To = 3, Z = 5 },
                        new BranchInput { Name = "c", From = 1, To = 2, Z = 10 },
                        new BranchInput { Name = "d", From = 2, To = 3, Z = 5 }
                    }
                },
                // Lecture 1 / 2: directed graph with 4 nodes and 6 branches (a..f), tree = d,e,f.
                new ExamplePreset
                {
                    Key = "lec1",
                    Title = "Lecture 1 - directed graph (6 branches, tree d,e,f)",
                    Mode = "incidence",
                    Tree = "d,e,f",
                    Reference = 4,
                    Matrix = "-1  1  0 -1  0  0\n 0 -1  1  0  1  0\n 1  0 -1  0  0  1\n 0  0  0  1 -1 -1",
                    Names = new List<string> { "a", "b", "c", "d", "e", "f" }
                },
                // Lecture 2 example: reduced incidence matrix, find B and C.
                new ExamplePreset
                {
                    Key = "lec2",
                    Title = "Lecture 2 - reduced A matrix (find B and C)",
                    Mode = "incidence",
                    Tree = "",
                    Matrix = "-1  1  1  1  0\n 0  0  0 -1  0\n 1 -1  0  0 -1",
                    Names = new List<string> { "a", "b", "c", "d", "e" }
                },
                new ExamplePreset
                {
                    Key = "bridge",
                    Title = "Wheatstone bridge (source + 5 resistors)",
                    Mode = "circuit",
                    Tree = "",
                    Reference = 4,
                    Branches = new List<BranchInput>
                    {
                        new BranchInput { Name = "s", From = 4, To = 1, Z = 1,  E = 10 },
                        new BranchInput { Name = "a", From = 1, To = 2, Z = 10 },
                        new BranchInput { Name = "b", From = 1, To = 3, Z = 20 },
                        new BranchInput { Name = "c", From = 2, To = 3, Z = 5 },
                        new BranchInput { Name = "d", From = 2, To = 4, Z = 30 },
                        new BranchInput { Name = "e", From = 3, To = 4, Z = 40 }
                    }
                }
            };
        }
    }
}
