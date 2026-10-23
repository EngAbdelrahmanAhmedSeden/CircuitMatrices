using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CircuitMatrices.Models;

namespace CircuitMatrices.Services
{
    public class AnalysisException : Exception
    {
        public AnalysisException(string message) : base(message) { }
    }

    /// <summary>
    /// Implements the material of CAD lectures 1-3:
    ///   graph counts (N, B, L), incidence / tie-set / cut-set matrices, number of trees,
    ///   KCL / KVL checks and the loop and cut-set network equilibrium equations.
    /// Sign convention (lecture 1): +1 = branch current LEAVES the node, -1 = ENTERS the node.
    /// </summary>
    public class GraphAnalyzerService : IGraphAnalyzerService
    {
        const double Tol = 1e-9;

        public AnalyzeResponse Analyze(AnalyzeRequest req)
        {
            var res = new AnalyzeResponse();
            try
            {
                Run(req, res);
            }
            catch (AnalysisException ex)
            {
                res.Error = ex.Message;
            }
            catch (Exception ex)
            {
                res.Error = "Unexpected error: " + ex.Message;
            }
            return res;
        }

        // ==================================================================
        // Main pipeline
        // ==================================================================
        static void Run(AnalyzeRequest req, AnalyzeResponse res)
        {
            if (req == null) throw new AnalysisException("Empty request.");

            string mode = string.IsNullOrWhiteSpace(req.Mode) ? "circuit" : req.Mode.Trim().ToLowerInvariant();
            if (mode != "circuit" && mode != "incidence" && mode != "tieset" && mode != "cutset")
                throw new AnalysisException("Unknown input mode '" + req.Mode + "'.");

            List<BranchInput> inputs = req.Branches ?? new List<BranchInput>();

            // ---- number of branches ------------------------------------------------
            int b;
            if (mode == "circuit")
            {
                b = inputs.Count;
            }
            else
            {
                CheckRectangular(req.Matrix);
                b = req.Matrix[0].Length;
            }
            if (b == 0) throw new AnalysisException("Add at least one branch.");

            // ---- branch names and values ------------------------------------------
            var names = new string[b];
            var z = new double[b];
            var e = new double[b];
            var cur = new double[b];
            var seen = new HashSet<string>();
            for (int k = 0; k < b; k++)
            {
                BranchInput bi = k < inputs.Count ? inputs[k] : null;
                string nm = (bi != null && !string.IsNullOrWhiteSpace(bi.Name)) ? bi.Name.Trim() : DefaultName(k);
                if (!seen.Add(nm)) throw new AnalysisException("Branch name '" + nm + "' is used twice. Branch names must be unique.");
                names[k] = nm;
                z[k] = bi != null ? bi.Z : 1.0;
                e[k] = bi != null ? bi.E : 0.0;
                cur[k] = bi != null ? bi.I : 0.0;
                if (double.IsNaN(z[k]) || double.IsInfinity(z[k]) || z[k] < 0)
                    throw new AnalysisException("Branch '" + nm + "': the impedance Z must be a number >= 0.");
                if (double.IsNaN(e[k]) || double.IsInfinity(e[k]) || double.IsNaN(cur[k]) || double.IsInfinity(cur[k]))
                    throw new AnalysisException("Branch '" + nm + "': E and I must be finite numbers.");
            }

            // ---- topology ----------------------------------------------------------
            int n;
            int[] src = null, to = null;
            int[] twigIdx, linkIdx;
            double[][] Bm, Cm;
            double[][] A = null;
            bool[] isTree = null;
            int refNode = 0;

            bool hasTopology = (mode == "circuit" || mode == "incidence");
            res.HasTopology = hasTopology;
            res.Branches = b;

            if (hasTopology)
            {
                int refRequested = req.Reference ?? 0;
                if (mode == "circuit")
                {
                    src = new int[b];
                    to = new int[b];
                    n = 0;
                    for (int k = 0; k < b; k++)
                    {
                        BranchInput bi = inputs[k];
                        if (bi.From < 1 || bi.To < 1)
                            throw new AnalysisException("Branch '" + names[k] + "': node numbers must be 1, 2, 3, ...");
                        if (bi.From == bi.To)
                            throw new AnalysisException("Branch '" + names[k] + "' starts and ends at node " + bi.From + " (a self-loop). Use two different nodes.");
                        src[k] = bi.From - 1;
                        to[k] = bi.To - 1;
                        n = Math.Max(n, Math.Max(bi.From, bi.To));
                    }
                    var used = new bool[n];
                    for (int k = 0; k < b; k++) { used[src[k]] = true; used[to[k]] = true; }
                    for (int i = 0; i < n; i++)
                        if (!used[i])
                            throw new AnalysisException("Node " + (i + 1) + " is not connected to any branch. Number the nodes 1.." + n + " without gaps.");

                    refNode = (refRequested >= 1 && refRequested <= n) ? refRequested : n;
                    if (refRequested != 0 && (refRequested < 1 || refRequested > n))
                        res.Warnings.Add("Reference node " + refRequested + " does not exist; node " + n + " was used instead.");
                    A = BuildIncidence(n, b, src, to);
                }
                else
                {
                    A = ParseIncidence(req.Matrix, refRequested, res.Warnings, out n, out refNode, out src, out to);
                }

                res.Nodes = n;
                res.ReferenceNode = refNode;

                int components = CountComponents(n, b, src, to);
                res.Connected = components == 1;
                if (components != 1)
                    throw new AnalysisException("The graph is not connected (" + components + " separate parts), so the number of trees is 0 and the lecture formulas do not apply. Connect the parts or analyse each part separately.");

                res.Links = b - n + 1;
                res.Twigs = n - 1;

                isTree = ChooseTree(n, b, src, to, names, req.Tree, res.Warnings);
                twigIdx = IndicesWhere(isTree, true);
                linkIdx = IndicesWhere(isTree, false);

                Bm = BuildTieSet(n, b, src, to, isTree, linkIdx);
                Cm = BuildCutSet(b, twigIdx, linkIdx, Bm);
            }
            else
            {
                if (mode == "tieset")
                    DeriveFromTieSet(req.Matrix, b, out linkIdx, out twigIdx, out Bm, out Cm);
                else
                    DeriveFromCutSet(req.Matrix, b, out linkIdx, out twigIdx, out Bm, out Cm);

                n = twigIdx.Length + 1;
                res.Nodes = n;
                res.Connected = true;
                res.Links = linkIdx.Length;
                res.Twigs = twigIdx.Length;
                isTree = new bool[b];
                foreach (int t in twigIdx) isTree[t] = true;
                res.Warnings.Add("Only a " + (mode == "tieset" ? "tie-set" : "cut-set") + " matrix was given, so the number of nodes is inferred as twigs + 1 and the incidence matrix / number of trees cannot be computed.");
            }

            res.Warnings.Add("Results do not depend on which tree is chosen; the tree only decides how B and C look.");

            foreach (int t in twigIdx) res.TreeBranches.Add(names[t]);
            foreach (int l in linkIdx) res.LinkBranches.Add(names[l]);

            // ---- display matrices (columns ordered [twigs | links], as in the lectures) ----
            var order = new List<int>();
            order.AddRange(twigIdx);
            order.AddRange(linkIdx);
            var orderNames = order.Select(i => names[i]).ToList();
            var nodeLabels = Enumerable.Range(1, n).Select(i => "node " + i).ToList();

            if (hasTopology)
            {
                res.A = MakeMatrix("A  (incidence matrix)", A, nodeLabels, names.ToList());

                var red = new List<double[]>();
                var redLabels = new List<string>();
                for (int i = 0; i < n; i++)
                {
                    if (i == refNode - 1) continue;
                    red.Add(A[i]);
                    redLabels.Add("node " + (i + 1));
                }
                res.Reduced = MakeMatrix("A_r  (reduced incidence matrix, reference = node " + refNode + ")", red.ToArray(), redLabels, names.ToList());

                for (int k = 0; k < b; k++)
                    res.Edges.Add(new GraphEdge { Name = names[k], From = src[k] + 1, To = to[k] + 1, IsTree = isTree[k] });
            }

            res.B = MakeMatrix("B  (fundamental tie-set matrix)", PermuteColumns(Bm, order), linkIdx.Select(i => "loop " + names[i]).ToList(), orderNames);
            res.C = MakeMatrix("C  (fundamental cut-set matrix)", PermuteColumns(Cm, order), twigIdx.Select(i => "cut " + names[i]).ToList(), orderNames);

            // ---- number of trees (Kirchhoff): det(A_r A_r^T) ------------------------------
            double[][] reducedA = null;
            if (hasTopology)
            {
                reducedA = res.Reduced.Data;
                double[][] M = Mat.Mul(reducedA, Mat.Transpose(reducedA, b));
                double det = Mat.Det(M);
                res.TreeCount = (int)Math.Round(det);

                bool skipped, truncated;
                res.Trees = EnumerateTrees(n, b, src, to, names, 200, out skipped, out truncated);
                res.TreesSkipped = skipped;
                res.TreesTruncated = !skipped && res.TreeCount.HasValue && res.TreeCount.Value > res.Trees.Count;
            }

            // ---- solve --------------------------------------------------------------------
            res.Loop = SolveLoop(Bm, b, z, e, cur);
            res.CutSet = SolveCutSet(Cm, b, z, e, cur);

            for (int k = 0; k < b; k++)
            {
                var br = new BranchResult
                {
                    Name = names[k],
                    From = hasTopology ? src[k] + 1 : 0,
                    To = hasTopology ? to[k] + 1 : 0,
                    IsTree = isTree[k],
                    Z = z[k],
                    E = e[k],
                    I = cur[k]
                };
                if (res.Loop.Ok) { br.LoopJ = res.Loop.J[k]; br.LoopV = res.Loop.V[k]; }
                if (res.CutSet.Ok) { br.CutJ = res.CutSet.J[k]; br.CutV = res.CutSet.V[k]; }
                res.Results.Add(br);
            }

            // ---- checks -------------------------------------------------------------------
            AddChecks(res, hasTopology ? A : null, Bm, Cm);

            // ---- step-by-step walk-through --------------------------------------------------
            BuildSteps(res, hasTopology, n, b, names, z, e, cur, Bm, Cm, twigIdx, linkIdx, orderNames, reducedA);
        }

        // ==================================================================
        // Input handling
        // ==================================================================
        static string DefaultName(int k)
        {
            if (k < 26) return ((char)('a' + k)).ToString();
            return "br" + (k + 1);
        }

        static void CheckRectangular(double[][] m)
        {
            if (m == null || m.Length == 0 || m[0] == null || m[0].Length == 0)
                throw new AnalysisException("Please enter a matrix (one row per line, numbers separated by spaces).");
            int cols = m[0].Length;
            for (int i = 0; i < m.Length; i++)
            {
                if (m[i] == null || m[i].Length != cols)
                    throw new AnalysisException("Every row of the matrix must have the same number of entries (row " + (i + 1) + " differs).");
                for (int j = 0; j < cols; j++)
                {
                    double v = m[i][j];
                    if (Math.Abs(v) > Tol && Math.Abs(v - 1) > Tol && Math.Abs(v + 1) > Tol)
                        throw new AnalysisException("Topology matrices may only contain +1, -1 and 0 (found " + Fmt(v) + " in row " + (i + 1) + ", column " + (j + 1) + ").");
                }
            }
        }

        static double[][] BuildIncidence(int n, int b, int[] src, int[] to)
        {
            double[][] a = Mat.Create(n, b);
            for (int k = 0; k < b; k++)
            {
                a[src[k]][k] = 1;     // current leaves the 'src' node
                a[to[k]][k] = -1;      // and enters the 'to' node
            }
            return a;
        }

        /// <summary>
        /// Accepts a full incidence matrix (every column has one +1 and one -1) or a reduced one
        /// (a reference row has been deleted, so some columns have a single non-zero entry).
        /// Returns the FULL matrix and derives the end nodes of every branch src it.
        /// </summary>
        static double[][] ParseIncidence(double[][] m, int refRequested, List<string> warnings,
                                         out int n, out int refNode, out int[] src, out int[] to)
        {
            int rows = m.Length;
            int b = m[0].Length;
            bool reduced = false;

            for (int j = 0; j < b; j++)
            {
                int pos = 0, neg = 0;
                for (int i = 0; i < rows; i++)
                {
                    if (m[i][j] > 0.5) pos++;
                    else if (m[i][j] < -0.5) neg++;
                }
                if (pos > 1 || neg > 1 || (pos + neg) == 0)
                    throw new AnalysisException("Column " + (j + 1) + " of the incidence matrix must contain one +1 (current leaves a node) and one -1 (current enters a node); in a reduced matrix one of the two may be missing.");
                if (pos + neg == 1) reduced = true;
            }

            double[][] full;
            if (reduced)
            {
                n = rows + 1;
                refNode = (refRequested >= 1 && refRequested <= n) ? refRequested : n;
                full = Mat.Create(n, b);
                for (int i = 0; i < rows; i++)
                {
                    int target = i < refNode - 1 ? i : i + 1;
                    for (int j = 0; j < b; j++) full[target][j] = m[i][j];
                }
                for (int j = 0; j < b; j++)
                {
                    double s = 0;
                    for (int i = 0; i < rows; i++) s += m[i][j];
                    full[refNode - 1][j] = -s;     // every column of a full matrix sums to zero
                }
                warnings.Add("Reduced incidence matrix detected: the missing reference row (node " + refNode + ") was rebuilt so that every column sums to zero.");
            }
            else
            {
                n = rows;
                refNode = (refRequested >= 1 && refRequested <= n) ? refRequested : n;
                full = Mat.Copy(m);
            }

            src = new int[b];
            to = new int[b];
            for (int j = 0; j < b; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (full[i][j] > 0.5) src[j] = i;
                    else if (full[i][j] < -0.5) to[j] = i;
                }
            }
            return full;
        }

        static void DeriveFromTieSet(double[][] m, int b, out int[] linkIdx, out int[] twigIdx, out double[][] Bm, out double[][] Cm)
        {
            int L = m.Length;
            var lc = new int[L];
            var isLink = new bool[b];
            for (int r = 0; r < L; r++)
            {
                lc[r] = -1;
                for (int j = 0; j < b; j++)
                {
                    if (isLink[j] || m[r][j] < 0.5) continue;
                    bool unit = true;
                    for (int rr = 0; rr < L; rr++)
                        if (rr != r && Math.Abs(m[rr][j]) > Tol) { unit = false; break; }
                    if (unit) { lc[r] = j; isLink[j] = true; break; }
                }
                if (lc[r] < 0)
                    throw new AnalysisException("A fundamental tie-set matrix must contain a unit (identity) sub-matrix: each row needs one link column that has a 1 in that row and 0 in every other row. Row " + (r + 1) + " has none.");
            }
            twigIdx = IndicesWhere(isLink, false);
            linkIdx = lc;
            if (twigIdx.Length < 1) throw new AnalysisException("The tie-set matrix has no tree branches (columns must outnumber rows).");

            Bm = Mat.Copy(m);
            Cm = BuildCutSet(b, twigIdx, linkIdx, Bm);
        }

        static void DeriveFromCutSet(double[][] m, int b, out int[] linkIdx, out int[] twigIdx, out double[][] Bm, out double[][] Cm)
        {
            int T = m.Length;
            var tc = new int[T];
            var isTwig = new bool[b];
            for (int r = 0; r < T; r++)
            {
                tc[r] = -1;
                for (int j = 0; j < b; j++)
                {
                    if (isTwig[j] || m[r][j] < 0.5) continue;
                    bool unit = true;
                    for (int rr = 0; rr < T; rr++)
                        if (rr != r && Math.Abs(m[rr][j]) > Tol) { unit = false; break; }
                    if (unit) { tc[r] = j; isTwig[j] = true; break; }
                }
                if (tc[r] < 0)
                    throw new AnalysisException("A fundamental cut-set matrix must contain a unit (identity) sub-matrix: each row needs one tree-branch column that has a 1 in that row and 0 in every other row. Row " + (r + 1) + " has none.");
            }
            twigIdx = tc;
            linkIdx = IndicesWhere(isTwig, false);

            Cm = Mat.Copy(m);
            Bm = Mat.Create(linkIdx.Length, b);
            for (int s = 0; s < linkIdx.Length; s++)
            {
                int lj = linkIdx[s];
                Bm[s][lj] = 1;
                for (int t = 0; t < T; t++) Bm[s][tc[t]] = -m[t][lj];     // C_L = -B_t^T  =>  B_t = -C_L^T
            }
        }

        // ==================================================================
        // Graph algorithms
        // ==================================================================
        static int[] IndicesWhere(bool[] flags, bool value)
        {
            var list = new List<int>();
            for (int i = 0; i < flags.Length; i++) if (flags[i] == value) list.Add(i);
            return list.ToArray();
        }

        static int Find(int[] parent, int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        static int CountComponents(int n, int b, int[] src, int[] to)
        {
            var p = Enumerable.Range(0, n).ToArray();
            int comps = n;
            for (int k = 0; k < b; k++)
            {
                int x = Find(p, src[k]), y = Find(p, to[k]);
                if (x != y) { p[x] = y; comps--; }
            }
            return comps;
        }

        /// <summary>A tree contains all nodes, is connected and has no loop (lecture 1).</summary>
        static bool[] ChooseTree(int n, int b, int[] src, int[] to, string[] names, List<string> requested, List<string> warnings)
        {
            var isTree = new bool[b];
            var parent = Enumerable.Range(0, n).ToArray();

            if (requested != null)
            {
                var wanted = requested.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
                if (wanted.Count > 0)
                {
                    if (wanted.Count != n - 1)
                        throw new AnalysisException("A tree of a graph with N = " + n + " nodes has exactly N - 1 = " + (n - 1) + " branches, but " + wanted.Count + " were given.");
                    foreach (string w in wanted)
                    {
                        int k = Array.IndexOf(names, w);
                        if (k < 0) throw new AnalysisException("Tree: there is no branch called '" + w + "'.");
                        if (isTree[k]) throw new AnalysisException("Tree: branch '" + w + "' is listed twice.");
                        int x = Find(parent, src[k]), y = Find(parent, to[k]);
                        if (x == y) throw new AnalysisException("Tree: branch '" + w + "' closes a loop. A tree must not contain any loop.");
                        parent[x] = y;
                        isTree[k] = true;
                    }
                    return isTree;
                }
            }

            // default: greedy spanning tree in the order the branches were entered
            for (int k = 0; k < b; k++)
            {
                int x = Find(parent, src[k]), y = Find(parent, to[k]);
                if (x != y) { parent[x] = y; isTree[k] = true; }
            }
            return isTree;
        }

        /// <summary>
        /// Fundamental tie-set matrix. Row for link j: the link itself (+1) plus the tree path that
        /// closes the loop. Entry is +1 if a branch is traversed in its own reference direction, else -1.
        /// </summary>
        static double[][] BuildTieSet(int n, int b, int[] src, int[] to, bool[] isTree, int[] linkIdx)
        {
            // root the tree at node 0 with a breadth-first search
            var parent = new int[n];
            var parentBr = new int[n];
            var depth = new int[n];
            var visited = new bool[n];
            var queue = new Queue<int>();
            visited[0] = true;
            parent[0] = -1;
            parentBr[0] = -1;
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                for (int k = 0; k < b; k++)
                {
                    if (!isTree[k]) continue;
                    int v;
                    if (src[k] == u) v = to[k];
                    else if (to[k] == u) v = src[k];
                    else continue;
                    if (visited[v]) continue;
                    visited[v] = true;
                    parent[v] = u;
                    parentBr[v] = k;
                    depth[v] = depth[u] + 1;
                    queue.Enqueue(v);
                }
            }

            double[][] B = Mat.Create(linkIdx.Length, b);
            for (int r = 0; r < linkIdx.Length; r++)
            {
                int j = linkIdx[r];
                B[r][j] = 1;                    // loop direction = link direction (src -> to)
                int x = to[j];                  // then return src 'to' back to 'src' through the tree
                int y = src[j];
                while (x != y)
                {
                    if (depth[x] >= depth[y])
                    {
                        int k = parentBr[x];    // travel child x -> parent
                        B[r][k] = (src[k] == x) ? 1 : -1;
                        x = parent[x];
                    }
                    else
                    {
                        int k = parentBr[y];    // travel parent -> child y
                        B[r][k] = (src[k] == parent[y]) ? 1 : -1;
                        y = parent[y];
                    }
                }
            }
            return B;
        }

        /// <summary>Lecture 2: C = [ U | C_L ] with C_L = -B_t^T (twig columns form the unit matrix).</summary>
        static double[][] BuildCutSet(int b, int[] twigIdx, int[] linkIdx, double[][] B)
        {
            double[][] C = Mat.Create(twigIdx.Length, b);
            for (int r = 0; r < twigIdx.Length; r++)
            {
                int t = twigIdx[r];
                C[r][t] = 1;
                for (int s = 0; s < linkIdx.Length; s++)
                    C[r][linkIdx[s]] = -B[s][t];
            }
            return C;
        }

        static bool IsAcyclic(int[] chosen, int n, int[] src, int[] to)
        {
            var p = Enumerable.Range(0, n).ToArray();
            foreach (int k in chosen)
            {
                int x = Find(p, src[k]), y = Find(p, to[k]);
                if (x == y) return false;
                p[x] = y;
            }
            return true;
        }

        /// <summary>Lists spanning trees (N-1 branches, no loop) when the search space is small enough.</summary>
        static List<string> EnumerateTrees(int n, int b, int[] src, int[] to, string[] names, int cap,
                                           out bool skipped, out bool truncated)
        {
            var list = new List<string>();
            truncated = false;
            int k = n - 1;

            double combos = 1;
            for (int i = 1; i <= k; i++) combos = combos * (b - k + i) / i;
            skipped = combos > 400000;
            if (skipped || k < 1 || k > b) return list;

            var idx = new int[k];
            for (int i = 0; i < k; i++) idx[i] = i;
            while (true)
            {
                if (IsAcyclic(idx, n, src, to))
                {
                    list.Add(string.Join(",", idx.Select(i => names[i])));
                    if (list.Count >= cap) { truncated = true; break; }
                }
                int p = k - 1;
                while (p >= 0 && idx[p] == b - k + p) p--;
                if (p < 0) break;
                idx[p]++;
                for (int q = p + 1; q < k; q++) idx[q] = idx[q - 1] + 1;
            }
            return list;
        }

        // ==================================================================
        // Network equilibrium equations (lecture 3)
        // ==================================================================

        /// <summary>
        /// Loop analysis:   B Z B^T I_L = B E - B Z I_s,   J_B = B^T I_L,   V_B = Z (J_B + I_s) - E
        /// </summary>
        static MethodResult SolveLoop(double[][] B, int b, double[] z, double[] e, double[] cur)
        {
            var r = new MethodResult();
            int L = B.Length;

            if (L == 0)
            {
                r.Ok = true;
                r.Note = "The graph has no independent loop (links = 0), so every branch current is zero.";
                r.Unknowns = new double[0];
                r.J = new double[b];
                r.V = new double[b];
                for (int k = 0; k < b; k++) r.V[k] = z[k] * cur[k] - e[k];
                return r;
            }

            double[][] Bt = Mat.Transpose(B, b);
            double[][] M = Mat.Mul(Mat.Mul(B, Mat.Diag(z)), Bt);

            var rhs = new double[L];
            for (int i = 0; i < L; i++)
            {
                double s = 0;
                for (int k = 0; k < b; k++) s += B[i][k] * (e[k] - z[k] * cur[k]);
                rhs[i] = s;
            }

            double[] il = Mat.Solve(M, rhs);
            if (il == null)
            {
                r.Ok = false;
                r.Error = "B Z B^T is singular (for example a loop made only of ideal voltage sources, Z = 0), so the loop currents are not unique.";
                return r;
            }

            r.Ok = true;
            r.Unknowns = il;
            r.J = Mat.MulTransposeVec(B, b, il);
            r.V = new double[b];
            for (int k = 0; k < b; k++) r.V[k] = z[k] * (r.J[k] + cur[k]) - e[k];
            return r;
        }

        /// <summary>
        /// Cut-set analysis:   C Y C^T V_T = C I_s - C Y E,   V_B = C^T V_T,   J_B = Y (V_B + E) - I_s
        /// </summary>
        static MethodResult SolveCutSet(double[][] C, int b, double[] z, double[] e, double[] cur)
        {
            var r = new MethodResult();
            int T = C.Length;

            var y = new double[b];
            for (int k = 0; k < b; k++)
            {
                if (z[k] < 1e-12)
                {
                    r.Ok = false;
                    r.Error = "The cut-set method needs the branch admittance Y = 1/Z, so every branch must have Z > 0 (branch " + (k + 1) + " has Z = 0). Use the loop method for ideal voltage sources.";
                    return r;
                }
                y[k] = 1.0 / z[k];
            }

            double[][] Ct = Mat.Transpose(C, b);
            double[][] M = Mat.Mul(Mat.Mul(C, Mat.Diag(y)), Ct);

            var rhs = new double[T];
            for (int i = 0; i < T; i++)
            {
                double s = 0;
                for (int k = 0; k < b; k++) s += C[i][k] * (cur[k] - y[k] * e[k]);
                rhs[i] = s;
            }

            double[] vt = Mat.Solve(M, rhs);
            if (vt == null)
            {
                r.Ok = false;
                r.Error = "C Y C^T is singular, so the node-pair voltages are not unique.";
                return r;
            }

            r.Ok = true;
            r.Unknowns = vt;
            r.V = Mat.MulTransposeVec(C, b, vt);
            r.J = new double[b];
            for (int k = 0; k < b; k++) r.J[k] = y[k] * (r.V[k] + e[k]) - cur[k];
            return r;
        }

        // ==================================================================
        // Verification
        // ==================================================================
        static void AddChecks(AnalyzeResponse res, double[][] A, double[][] B, double[][] C)
        {
            MethodResult m = res.Loop.Ok ? res.Loop : (res.CutSet.Ok ? res.CutSet : null);
            if (m == null) return;

            double scale = Math.Max(1.0, Math.Max(Mat.MaxAbs(m.J), Mat.MaxAbs(m.V)));
            Action<string, double> add = (label, value) =>
                res.Checks.Add(new CheckItem { Label = label, Value = value, Passed = Math.Abs(value) <= 1e-7 * scale });

            if (A != null) add("KCL:  max |A · J_B|", Mat.MaxAbs(Mat.MulVec(A, m.J)));
            add("KCL:  max |C · J_B|", Mat.MaxAbs(Mat.MulVec(C, m.J)));
            if (B.Length > 0) add("KVL:  max |B · V_B|", Mat.MaxAbs(Mat.MulVec(B, m.V)));

            double tellegen = 0;
            for (int k = 0; k < m.J.Length; k++) tellegen += m.V[k] * m.J[k];
            add("Tellegen:  Σ V_B · J_B  (power balance)", tellegen);

            if (res.Loop.Ok && res.CutSet.Ok)
            {
                double d = 0;
                for (int k = 0; k < res.Loop.J.Length; k++)
                {
                    d = Math.Max(d, Math.Abs(res.Loop.J[k] - res.CutSet.J[k]));
                    d = Math.Max(d, Math.Abs(res.Loop.V[k] - res.CutSet.V[k]));
                }
                add("Loop method vs cut-set method:  max difference", d);
            }
        }

        // ==================================================================
        // Presentation helpers
        // ==================================================================
        static string Fmt(double x)
        {
            if (Math.Abs(x) < 1e-12) return "0";
            return Math.Round(x, 6).ToString("0.######", CultureInfo.InvariantCulture);
        }

        static NamedMatrix MakeMatrix(string label, double[][] data, List<string> rowLabels, List<string> colLabels)
        {
            return new NamedMatrix { Label = label, Data = data, RowLabels = rowLabels, ColLabels = colLabels };
        }

        static double[][] PermuteColumns(double[][] m, List<int> order)
        {
            var r = Mat.Create(m.Length, order.Count);
            for (int i = 0; i < m.Length; i++)
                for (int j = 0; j < order.Count; j++)
                    r[i][j] = m[i][order[j]];
            return r;
        }

        static NamedMatrix Column(string label, double[] v, List<string> rowLabels, string colLabel)
        {
            var d = Mat.Create(v.Length, 1);
            for (int i = 0; i < v.Length; i++) d[i][0] = v[i];
            return MakeMatrix(label, d, rowLabels, new List<string> { colLabel });
        }

        static void BuildSteps(AnalyzeResponse res, bool hasTopology, int n, int b, string[] names,
                               double[] z, double[] e, double[] cur, double[][] B, double[][] C,
                               int[] twigIdx, int[] linkIdx, List<string> orderNames, double[][] reducedA)
        {
            var nameList = names.ToList();

            // 1. counting
            res.Steps.Add(new Step
            {
                Title = "Count nodes, branches and links",
                Text = "Nodes N = " + n + ",  branches B = " + b + ".\n" +
                       "A tree has N − 1 = " + (n - 1) + " branches (twigs).\n" +
                       "Number of links = B − (N − 1) = B − N + 1 = " + b + " − " + n + " + 1 = " + (b - n + 1) + ".\n" +
                       "Tree branches: " + string.Join(", ", res.TreeBranches) + ".   Links: " + (res.LinkBranches.Count > 0 ? string.Join(", ", res.LinkBranches) : "none") + "."
            });

            // 2. incidence matrix + KCL
            if (hasTopology)
            {
                var s2 = new Step
                {
                    Title = "Incidence matrix A and the reduced matrix A_r",
                    Text = "Row = node, column = branch. +1 if the branch current leaves the node, −1 if it enters, 0 otherwise. " +
                           "Deleting the row of the reference node (node " + res.ReferenceNode + ") gives the reduced matrix A_r with N − 1 rows. " +
                           "KCL in matrix form:  A · J_B = 0."
                };
                s2.Matrices.Add(res.A);
                s2.Matrices.Add(res.Reduced);
                res.Steps.Add(s2);
            }

            // 3. tie-set
            var s3 = new Step
            {
                Title = "Fundamental tie-set matrix B (loops)",
                Text = "Every link closes exactly one fundamental loop with the tree, so B has one row per link (" + linkIdx.Length + " rows). " +
                       "Entry +1 for the link itself; for the other branches +1 if the branch current has the same direction as the loop (link) current, −1 if opposite, 0 if not in the loop. " +
                       "Columns are ordered [twigs | links], so the links form a unit matrix.  KVL:  B · V_B = 0,   J_B = Bᵀ · I_L."
            };
            s3.Matrices.Add(res.B);
            res.Steps.Add(s3);

            // 4. cut-set
            var s4 = new Step
            {
                Title = "Fundamental cut-set matrix C",
                Text = "Every twig defines one fundamental cut-set, so C has one row per twig (" + twigIdx.Length + " rows). " +
                       "C = [ U | C_L ] with C_L = −B_tᵀ (B_t = the twig part of the tie-set matrix).  KCL:  C · J_B = 0,   V_B = Cᵀ · V_T."
            };
            s4.Matrices.Add(res.C);
            res.Steps.Add(s4);

            // 5. trees
            if (hasTopology && res.TreeCount.HasValue)
            {
                var s5 = new Step
                {
                    Title = "Number of trees",
                    Text = "Number of trees = det( A_r · A_rᵀ ) = " + res.TreeCount.Value + ".\n" +
                           "(Matrix-tree theorem: A_r · A_rᵀ is the reduced Laplacian of the graph.)"
                };
                res.Steps.Add(s5);
            }

            // 6. branch relation
            var s6 = new Step
            {
                Title = "Branch equation",
                Text = "For a branch with source voltage E, source current I and impedance Z:\n" +
                       "V_B = Z_B (J_B + I_s) − E_s        and        J_B = Y_B (V_B + E_s) − I_s    (Y = Z⁻¹)."
            };
            s6.Matrices.Add(Column("Z (Ω)", z, nameList, "Z"));
            s6.Matrices.Add(Column("E_s (V)", e, nameList, "E"));
            s6.Matrices.Add(Column("I_s (A)", cur, nameList, "I"));
            res.Steps.Add(s6);

            // 7. loop method
            var s7 = new Step { Title = "Loop equations (tie-set method)" };
            if (res.Loop.Ok)
            {
                s7.Text = "Substitute V_B = Z(J_B + I_s) − E into B·V_B = 0 and J_B = Bᵀ·I_L:\n" +
                          "B Z Bᵀ · I_L = B E_s − B Z I_s.  Solve for the loop currents I_L, then J_B = Bᵀ I_L and V_B = Z (J_B + I_s) − E_s.";
                if (res.Loop.Note != null) s7.Text += "\n" + res.Loop.Note;
                var loopNames = linkIdx.Select(i => "I_L(" + names[i] + ")").ToList();
                s7.Matrices.Add(Column("I_L (A)", res.Loop.Unknowns, loopNames, "value"));
                s7.Matrices.Add(Column("J_B (A)", res.Loop.J, nameList, "value"));
                s7.Matrices.Add(Column("V_B (V)", res.Loop.V, nameList, "value"));
            }
            else
            {
                s7.Text = res.Loop.Error;
            }
            res.Steps.Add(s7);

            // 8. cut-set method
            var s8 = new Step { Title = "Cut-set equations (node-pair method)" };
            if (res.CutSet.Ok)
            {
                s8.Text = "Substitute J_B = Y(V_B + E) − I_s into C·J_B = 0 and V_B = Cᵀ·V_T:\n" +
                          "C Y Cᵀ · V_T = C I_s − C Y E_s.  Solve for the node-pair (twig) voltages V_T, then V_B = Cᵀ V_T and J_B = Y (V_B + E_s) − I_s.";
                var twigNames = twigIdx.Select(i => "V_T(" + names[i] + ")").ToList();
                s8.Matrices.Add(Column("V_T (V)", res.CutSet.Unknowns, twigNames, "value"));
                s8.Matrices.Add(Column("V_B (V)", res.CutSet.V, nameList, "value"));
                s8.Matrices.Add(Column("J_B (A)", res.CutSet.J, nameList, "value"));
            }
            else
            {
                s8.Text = res.CutSet.Error;
            }
            res.Steps.Add(s8);

            // 9. verification
            if (res.Checks.Count > 0)
            {
                var lines = res.Checks.Select(c => (c.Passed ? "✓ " : "✗ ") + c.Label + " = " + c.Value.ToString("0.###e+0", CultureInfo.InvariantCulture));
                res.Steps.Add(new Step
                {
                    Title = "Verify the answer",
                    Text = "A correct solution must satisfy KCL, KVL and power balance, and both methods must agree:\n" + string.Join("\n", lines)
                });
            }
        }
    }
}
