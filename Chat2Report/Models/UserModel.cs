using System.Text.Json.Serialization;

namespace Chat2Report.Models
{
    public class UserModel
    {
        public string UserName { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }

        [JsonIgnore]
        public List<string> Groups { get; set; }
    }
}