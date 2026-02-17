The user's query is:
---
{{{resolved_user_query}}}
---

The chart to be created has the following properties:
- Chart Type: **{{{chart_type}}}**
- Rendering Library: **{{{library}}}**

The data is about:
---
{{toJSON relevant_domains}}
---

The schema and column descriptions:
---
{{toJSON relevant_columns}}
---

Here is the data:
---
{{toJSON data}}
---

Your task is to generate the chart options. Your response **MUST** be a single JSON object within a markdown block ```json...```. Do not include any other text or explanations.

### CRITICAL FORMATTING RULES:
1. The "Options" value MUST be a single-line JSON string.
2. Replace all newlines (actual line breaks) with the `\n` escape sequence.
3. Escape all double quotes (") inside the Mermaid configuration or code with a backslash (\").
4. IMPORTANT:**YAML Front-matter `---`(if used):**   must be followed by a newline like this: `---\n`. 
5. Do NOT include any physical line breaks (0x0A) inside the "Options" field.

The structure of your response must be:
```json
{
  "Library": "{{{library}}}",
  "ChartType": "{{{chart_type}}}",
  "Options": "{{{options_example}}}"
}
```
