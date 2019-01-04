using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace GrammarInduction.Controllers
{
    [Produces("application/json")]
    [Route("api/SyntaxInduction")]
    public class GrammarInductionController : Controller
    {
        public void Learn(string fileName)
        {

        }
    }
}