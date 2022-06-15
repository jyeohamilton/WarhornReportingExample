using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WarhornReporting
{
    /// <summary>
    /// Executes the query in GraphQLQuery and deserializes the response JSON into an object.
    /// </summary>
    internal class GraphQuery
    {
        public GraphQuery(string query, string eventSlug, DateTime start, DateTime end)
        {
            Query = query;
            Variables = new Dictionary<string, string>
                {
                    {"eventSlug", eventSlug},
                    {"start", start.ToString("o") },
                    {"end", end.ToString("o") },
                };
        }

        public string Query { get; set; }
        public Dictionary<string, string> Variables { get; set; }

        public static async Task<GraphQueryResponse> GetGraphInfo(HttpClient client, string eventSlug, DateTime start, DateTime end)
        {
            var gq = new GraphQuery(GraphQLQuery, eventSlug, start, end);
            var serializedQuery = JsonSerializer.Serialize(gq, SerializerOptions);

            var message = CreateMessage(serializedQuery);
            var response = await client.SendAsync(message);

            var jsonString = await response.Content.ReadAsStringAsync();
            var gqr = JsonSerializer.Deserialize<GraphQueryResponse>(jsonString, SerializerOptions);

            if (gqr is null)
            {
                throw new InvalidDataException("json can't be parsed into GraphQueryResponse");
            }

            return gqr;
        }

        private static HttpRequestMessage CreateMessage(string serializedQuery)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, GraphEndpointPath);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
            message.Content = new StringContent(serializedQuery, Encoding.UTF8, JsonContentType);

            return message;
        }

        private const string GraphEndpointPath = "graphql";
        private const string JsonContentType = "application/json";

        private static readonly string GraphQLQuery = File.ReadAllText("query.graphql");

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }
}