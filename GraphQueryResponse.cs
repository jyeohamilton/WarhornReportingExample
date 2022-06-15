namespace WarhornReporting
{
    public record class GraphQueryResponse
    {
        public DataType? Data { get; set; }

        public class Campaign
        {
            public string? Name { get; set; }
        }

        public class DataType
        {
            public EventSessions? EventSessions { get; set; }
        }

        public class EventSessions
        {
            public List<Node>? Nodes { get; set; }
        }

        public class GmSignup
        {
            public User? User { get; set; }
        }

        public class Node
        {
            public DateTime? StartsAt { get; set; }
            public DateTime? EndsAt { get; set; }
            public string? Status { get; set; }
            public Scenario? Scenario { get; set; }
            public Slot? Slot { get; set; }
            public string? Uuid { get; set; }
            public List<PlayerSignup>? PlayerSignups { get; set; }
            public List<GmSignup>? GmSignups { get; set; }
        }

        public class PlayerSignup
        {
            public User? User { get; set; }
        }

        public class Scenario
        {
            public string? Name { get; set; }
            public Campaign? Campaign { get; set; }
        }

        public class Slot
        {
            public Venue? Venue { get; set; }
        }

        public class User
        {
            public string? Id { get; set; }
        }

        public class Venue
        {
            public string? Name { get; set; }
        }
    }
}
