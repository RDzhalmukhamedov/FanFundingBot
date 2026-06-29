using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace FanFundingBot.App.Controllers;

[ExcludeFromCodeCoverage]
public class AppController : Controller
{
    [HttpGet]
    public string Index() => "It's Fan Funding";
}
