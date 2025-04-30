using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace growmesh_API.Services
{
    public class LlamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "http://localhost:8321"; // FastAPI server URL

        public LlamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> SendHomeAgentMessageAsync(string message, List<object> allGoalsData, List<object> trendData)
        {
            var requestBody = new { message, all_goals_data = allGoalsData, trend_data = trendData };
            return await PostAsync("/home-agent", requestBody);
        }

        public async Task<string> SendAllGoalsAgentMessageAsync(string message, List<object> allGoalsData)
        {
            var requestBody = new { message, all_goals_data = allGoalsData };
            return await PostAsync("/all-goals-agent", requestBody);
        }

        public async Task<string> SendGoalDetailsAgentMessageAsync(string message, object goalData, List<object> trendData, List<object> transactions)
        {
            var requestBody = new { message, goal_data = goalData, trend_data = trendData, transactions };
            return await PostAsync("/goal-details-agent", requestBody);
        }

        public async Task<string> SendProfileAgentMessageAsync(string message, object userData)
        {
            var requestBody = new { message, user_data = userData };
            return await PostAsync("/profile-agent", requestBody);
        }

        public async Task<string> SendTransactionsAgentMessageAsync(string message, List<object> allTransactions)
        {
            var requestBody = new { message, all_transactions = allTransactions };
            return await PostAsync("/transactions-agent", requestBody);
        }

        private async Task<string> PostAsync(string endpoint, object requestBody)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );
            var response = await _httpClient.PostAsync($"{_baseUrl}{endpoint}", content);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);
            return jsonResponse.GetProperty("response").GetString();
        }
    }
}