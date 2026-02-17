

The user's query is:
---
{{{resolved_user_query}}}
---

The data is about:
---
{{toJSON relevant_domains}}
---

The schema and column descriptions:
---
{{toJSON relevant_columns}}
---

Here is a the data:
---
{{toJSON data}}
---

Your task is to analyze the user query, the data context, and the data sample to create the most compatible and informative ASCII diagram. You are free to use any style of ASCII diagram you think is best for the situation (e.g., bar chart, timeline, table, etc.).

Respond with a single JSON object within a markdown block ```json...```. Do not include any other text or explanations.

```json
{
  "chart_type": "ASCII",
  "text": "<your generated ASCII diagram here as a string>"
}
```