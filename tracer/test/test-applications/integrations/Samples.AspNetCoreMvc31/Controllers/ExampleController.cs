using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WebService.Models;
using WebService.Repositories;
using WebService.Services;

namespace WebService.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ExampleController : ControllerBase
    {

        private readonly ILogger<CoursesController> _logger;
        private readonly IRoomService _roomService;

        public ExampleController(ILogger<CoursesController> logger, IRoomService roomService)
        {
            _logger = logger;
            _roomService = roomService;
        }

        [HttpGet("guess")]
        public int Guess()
        {
            var random = new System.Random();

            return SnapshotFeatures.GuessNumber(1, 100, random.Next(1, 100));
        }

        [HttpGet("guess-recursive")]
        public int GuessRecursive()
        {
            var random = new System.Random();

            return SnapshotFeatures.GuessNumberRecursive(1, 100, random.Next(1, 100));
        }
    }
}
