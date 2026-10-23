# Circuit Graph Analyzer - ASP.NET Core MVC (CAD course project)

Back end: ASP.NET Core **MVC** (.NET 8) - Controller + Razor Views + Models + Services.
Front end: HTML / CSS / JavaScript (in the Razor views and `wwwroot`).

## Run
1. Install the .NET 8 SDK (https://dotnet.microsoft.com/download).
2. In this folder:  `dotnet run`
3. Open http://localhost:5080

## MVC structure
```
CircuitMatrices/
├─ Program.cs                      MVC setup, dependency injection, default route {controller=Home}/{action=Index}
├─ Controllers/
│   └─ HomeController.cs           Index (GET), Theory (GET), Analyze (POST, JSON)
├─ Models/
│   └─ CircuitModels.cs            AnalyzeRequest, AnalyzeResponse, NamedMatrix, ExamplePreset, HomeViewModel ...
├─ Services/
│   ├─ IGraphAnalyzerService.cs    interface injected into the controller
│   ├─ GraphAnalyzerService.cs     ALL the maths (A, A_r, B, C, N/B/L, trees, loop + cut-set equations, J_B, V_B, checks, steps)
│   ├─ Mat.cs                      matrix helpers (multiply, transpose, solve, determinant)
│   └─ ExampleCatalog.cs           lecture examples shown in "Load example"
├─ Views/
│   ├─ _ViewImports.cshtml, _ViewStart.cshtml
│   ├─ Shared/_Layout.cshtml       header, navigation, footer
│   └─ Home/
│       ├─ Index.cshtml            the Analyzer page (input forms, results)
│       └─ Theory.cshtml           graph-theory summary, formulas, exam checklist
└─ wwwroot/
    ├─ css/site.css
    └─ js/site.js                  reads the form, POSTs to /Home/Analyze, draws graph / matrices / steps
```
Request flow: `Index.cshtml` -> `site.js` sends JSON to `HomeController.Analyze` -> `IGraphAnalyzerService.Analyze` -> JSON back -> `site.js` renders it.

## Input modes
* **Branch list** - nodes, Z, E, I per branch (draw the circuit as a graph first).
* **Incidence matrix A** - full, or reduced (reference row deleted).
* **Tie-set matrix B** or **Cut-set matrix C** - the other matrices and the equations are derived from it.
* The circuit **image** is shown next to the form as a reference for reading nodes and values; it is not recognised automatically.

## Formulas (from the lectures)
* Links L = B - N + 1, twigs = N - 1;  number of trees = det(A_r A_r^T)
* KCL: A J_B = 0, C J_B = 0     KVL: B V_B = 0     J_B = B^T I_L     V_B = C^T V_T
* Branch: V_B = Z (J_B + I_s) - E_s      J_B = Y (V_B + E_s) - I_s
* Loop method:    B Z B^T I_L = B E_s - B Z I_s
* Cut-set method: C Y C^T V_T = C I_s - C Y E_s
* Sign in A: +1 current leaves the node, -1 enters.  C = [U | C_L], C_L = -B_t^T.

## Notes
* The cut-set method needs Z > 0 on every branch (Y = 1/Z). With an ideal voltage source (Z = 0) use the loop method.
* A source branch is one branch (source in series with its resistor), as in the Lecture 3 example (a = 10 V + 5 ohm).
