# Circuit Graph Analyzer - ASP.NET Core MVC (CAD course project)

Back end: ASP.NET Core **MVC** (.NET 10.0 - Controller + Razor Views + Models + Services.
Front end: HTML / CSS / JavaScript (in the Razor views and `wwwroot`).

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
