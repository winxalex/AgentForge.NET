using Chat2Report.Models;
using Chat2Report.Options;
using Chat2Report.Utilities;
using Microsoft.Extensions.Options; // Потребно за IOptions
using System.DirectoryServices;
using System.Text;




namespace Chat2Report.Services
{
    public class ADService : IADService
    {
        private readonly ADSettings _adSettings;

        // Користиме Dependency Injection за да ги добиеме поставките
        public ADService(IOptions<ADSettings> adSettingsOptions)
        {
            _adSettings = adSettingsOptions.Value;
        }

        public async Task<List<UserModel>> FindUsersByNameAsync(string? firstName, string? lastName, string? groupName = null)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                {
                    return new List<UserModel>();
                }
                
                string? groupDistinguishedName = null;
                // Чекор 1: Ако е побарано филтрирање по група, прво најди ја групата
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    groupDistinguishedName = FindGroupDistinguishedName(groupName);
                    // Ако групата не е пронајдена, врати празна листа веднаш
                    if (groupDistinguishedName == null)
                    {
                        return new List<UserModel>();
                    }
                }

                var usersFound = new List<UserModel>();
                try
                {
                    using var entry = new DirectoryEntry(_adSettings.LdapPath);
                    using var searcher = new DirectorySearcher(entry)
                    {
                        ReferralChasing = ReferralChasingOption.All,
                        PageSize = 1000
                    };

                    // Чекор 2: Конструирај го главниот LDAP филтер
                    var filter = BuildLdapFilter(firstName, lastName, groupDistinguishedName);
                    searcher.Filter = filter;

                    searcher.PropertiesToLoad.AddRange(new[] { "givenName", "sn", "mail", "samAccountName", "displayName", "memberOf" });

                    using (SearchResultCollection results = searcher.FindAll())
                    {
                        foreach (SearchResult result in results)
                        {
                            usersFound.Add(new UserModel
                            {
                                FirstName = GetStringProperty(result, "givenName"),
                                LastName = GetStringProperty(result, "sn"),
                                Email = GetStringProperty(result, "mail"),
                                UserName = GetStringProperty(result, "samAccountName"),
                                DisplayName = GetStringProperty(result, "displayName"),
                                Groups = GetStringCollectionProperty(result, "memberOf")
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Логирај грешка...
                    throw new Exception("Грешка при пребарување на Active Directory.", ex);
                }

                if (!usersFound.Any())
                {
                    return usersFound;
                }
                
                // Чекор 3: Сортирај ги резултатите по релевантност користејќи Левенштајн
                var sortedUsers = usersFound.OrderBy(user =>
                    CalculateRelevanceScore(user, firstName, lastName)
                ).ToList();

                return sortedUsers;
            });
        }

        #region Private Helper Methods


        private string BuildLdapFilter(string? firstName, string? lastName, string? groupDn)
        {
            var filterParts = new List<string> { "(&(objectCategory=person)(objectClass=user)" };

            // Филтер за име: Комбинира prefix, suffix и contains со OR
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                string sanitizedFirstName = SanitizeLdapFilter(firstName);
                filterParts.Add($"(|(givenName={sanitizedFirstName}*)(givenName=*{sanitizedFirstName})(givenName=*{sanitizedFirstName}*))");
            }

            // Филтер за презиме: Комбинира prefix, suffix и contains со OR
            if (!string.IsNullOrWhiteSpace(lastName))
            {
                string sanitizedLastName = SanitizeLdapFilter(lastName);
                filterParts.Add($"(|(sn={sanitizedLastName}*)(sn=*{sanitizedLastName})(sn=*{sanitizedLastName}*))");
            }

            // Филтер за група останува ист
            if (!string.IsNullOrWhiteSpace(groupDn))
            {
                filterParts.Add($"(memberOf:1.2.840.113556.1.4.1941:={SanitizeLdapFilter(groupDn)})");
            }

            filterParts.Add("(!userAccountControl:1.2.840.113556.1.4.803:=2))"); // Само активни корисници

            return string.Join("", filterParts);
        }

      

        private string? FindGroupDistinguishedName(string groupName)
        {
            try
            {
                using var entry = new DirectoryEntry(_adSettings.LdapPath);
                using var searcher = new DirectorySearcher(entry)
                {
                    Filter = $"(&(objectCategory=group)(sAMAccountName={SanitizeLdapFilter(groupName)}))"
                };
                searcher.PropertiesToLoad.Add("distinguishedName");

                SearchResult? result = searcher.FindOne();
                return GetStringProperty(result, "distinguishedName");
            }
            catch
            {
                // Грешка при пребарување на групата, врати null
                return null;
            }
        }
        
        private int CalculateRelevanceScore(UserModel user, string? queryFirstName, string? queryLastName)
        {
            int firstNameDistance = 0;
            int lastNameDistance = 0;

            if (!string.IsNullOrWhiteSpace(queryFirstName) && !string.IsNullOrWhiteSpace(user.FirstName))
                firstNameDistance = GeneratorUtil.LevenshteinDistance(user.FirstName.ToLower(), queryFirstName.ToLower());

            if (!string.IsNullOrWhiteSpace(queryLastName) && !string.IsNullOrWhiteSpace(user.LastName))
                lastNameDistance = GeneratorUtil.LevenshteinDistance(user.LastName.ToLower(), queryLastName.ToLower());
            
            return firstNameDistance + lastNameDistance;
        }



        // ... останатите помошни методи (GetStringProperty, SanitizeLdapFilter) остануваат исти ...
        private string? GetStringProperty(SearchResult result, string propertyName)
        {
            if (result.Properties.Contains(propertyName) && result.Properties[propertyName].Count > 0)
                return result.Properties[propertyName][0]?.ToString();
            return null;
        }

        private List<string> GetStringCollectionProperty(SearchResult result, string propertyName)
        {
            var values = new List<string>();
            if (result.Properties.Contains(propertyName))
            {
                foreach (var value in result.Properties[propertyName])
                {
                    values.Add(value.ToString());
                }
            }
            return values;
        }
        private string SanitizeLdapFilter(string filter)
        {
            var sb = new StringBuilder();
            foreach (char c in filter)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\5c"); break;
                    case '*': sb.Append("\\2a"); break;
                    case '(': sb.Append("\\28"); break;
                    case ')': sb.Append("\\29"); break;
                    case '\0': sb.Append("\\00"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        #endregion
    }
}