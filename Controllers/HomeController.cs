using CircuitMatrices.Models;
using CircuitMatrices.Services;
using Microsoft.AspNetCore.Mvc;

namespace CircuitMatrices.Controllers
{
    public class HomeController : Controller
    {
        private readonly IGraphAnalyzerService _analyzer;

        public HomeController(IGraphAnalyzerService analyzer)
        {
            _analyzer = analyzer;
        }

        // GET /  -> Views/Home/Index.cshtml (the analyzer page)
        [HttpGet]
        public IActionResult Index()
        {
            var model = new HomeViewModel { Examples = ExampleCatalog.All() };
            return View(model);
        }

        // GET /Home/Theory  -> Views/Home/Theory.cshtml (formulas and exam notes)
        [HttpGet]
        public IActionResult Theory()
        {
            return View();
        }

        // POST /Home/Analyze  <- JSON from wwwroot/js/site.js, -> JSON with matrices, trees, J_B, V_B ...
        [HttpPost]
        public IActionResult Analyze([FromBody] AnalyzeRequest request)
        {
            if (request == null)
            {
                return BadRequest(new AnalyzeResponse { Error = "Empty request." });
            }
            AnalyzeResponse result = _analyzer.Analyze(request);
            return Json(result);
        }
    }
}
