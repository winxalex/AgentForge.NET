 // --- START OF FILE DynamicHtmlContent.cs ---
 using Microsoft.Extensions.AI;
 using System.Text.Json.Serialization;
 
 namespace Chat2Report.Models
 {
     /// <summary>
     /// Represents a dynamic HTML content block to be rendered in the UI.
    
     /// </summary>
     public class DynamicHtmlContent : AIContent
     {
         /// <summary>
         /// The HTML content to be rendered.
         /// </summary>
         [JsonPropertyName("html")]
         public string Html { get; }
 
         /// <summary>
        
         /// </summary>
         [JsonPropertyName("type")]
         public string Type => "dynamic_html";
 
         public DynamicHtmlContent(string html) : base()
         {
             Html = html;
         }
     }
 }
 // --- END OF FILE DynamicHtmlContent.cs ---