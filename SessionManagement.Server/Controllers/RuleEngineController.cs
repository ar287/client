using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SessionManagement.Server.Services;
using SessionManagement.Shared.DTOs;

namespace SessionManagement.Server.Controllers
{
    public class NlRuleRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/[controller]")]
    public class RuleEngineController : ControllerBase
    {
        private readonly RuleEngineService _ruleEngine;
        private readonly EventService _eventService;
        private readonly AIService _aiService;

        public RuleEngineController(RuleEngineService ruleEngine, EventService eventService, AIService aiService)
        {
            _ruleEngine = ruleEngine;
            _eventService = eventService;
            _aiService = aiService;
        }

        // GET: api/ruleengine/rules
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules()
        {
            var rules = await _ruleEngine.GetRulesAsync();
            return Ok(rules);
        }

        // POST: api/ruleengine/evaluate/{clientId}
        [HttpPost("evaluate/{clientId}")]
        public async Task<IActionResult> EvaluateRisk(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return BadRequest(new { message = "ClientId is required." });

            var assessment = await _ruleEngine.EvaluateClientRiskAsync(clientId);
            return Ok(assessment);
        }

        // POST: api/ruleengine/ai-evaluate/{clientId}
        [HttpPost("ai-evaluate/{clientId}")]
        public async Task<IActionResult> EvaluateRiskWithAi(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                return BadRequest(new { message = "ClientId is required." });

            var ruleAssessment = await _ruleEngine.EvaluateClientRiskAsync(clientId);
            var timeline = await _eventService.GetTimelineAsync(new EventFilterRequest { ClientId = clientId, PageSize = 20 });
            var aiAssessment = await _aiService.AnalyzeClientRiskWithAiAsync(clientId, ruleAssessment, timeline.Events);

            return Ok(aiAssessment);
        }

        // POST: api/ruleengine/nl-rule-draft
        [HttpPost("nl-rule-draft")]
        public async Task<IActionResult> GenerateNlRuleDraft([FromBody] NlRuleRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest(new { message = "Prompt cannot be empty." });

            var draft = await _aiService.GenerateNaturalLanguageRuleDraftAsync(request.Prompt);
            return Ok(draft);
        }

        // POST: api/ruleengine/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateRule([FromBody] SecurityRuleDto rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.RuleName))
                return BadRequest(new { message = "Rule parameters incomplete." });

            bool success = await _ruleEngine.CreateRuleAsync(rule);
            return Ok(new { success, message = success ? "Rule created successfully." : "Failed to create rule." });
        }

        // POST: api/ruleengine/rules  (alias used by Admin WPF client)
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRuleAlias([FromBody] SecurityRuleDto rule)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.RuleName))
                return BadRequest(new { message = "Rule parameters incomplete." });

            bool success = await _ruleEngine.CreateRuleAsync(rule);
            return Ok(new { success, message = success ? "Rule created successfully." : "Failed to create rule." });
        }
    }
}
