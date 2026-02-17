
The user's query is:
---
{{{resolved_user_query}}}
---

Context:
---
{{toJSON relevant_domains}}

---
{{toJSON relevant_columns}} 

Here is a sample of the data:
---
{{toJSON data.[0]}}
---

The available and supported chart types are:
---
| Chart Type | Usage |
|------------|-------|
{{#each supported_charts}}
| {{Type}} | {{Usage}} |
{{/each}}
---

Analyze the user's query.

1.  **If the user explicitly requests a chart type:**
    *   Check if the requested type is in the supported graphical list (e.g., {{#each supported_charts}}`{{Type}}`{{#unless @last}}, {{/unless}}{{/each}}).
    *   If it **is** supported, you **MUST** select it.
    *   If it is **NOT** supported, you **MUST** select `'ascii'` as a fallback. Do not suggest alternatives.

2.  **If the user does NOT request a specific chart type:**
    *   Analyze the query and data to determine if any of the supported graphical charts (e.g., {{#each supported_charts}}`{{Type}}`{{#unless @last}}, {{/unless}}{{/each}}) are a good fit.
    *   If a suitable graphical chart is found, select it.
    *   If no supported graphical chart is suitable, you **MUST** select `'ascii'`.


### CRITICAL OUTPUT RULES:
1.  **Output ONLY the JSON object inside a markdown block.**
2.  **Do NOT include any introductory text (preamble).**
3.  **Do NOT include any concluding text (postscript).**
4.  **Do NOT include any explanations outside the JSON.**

Respond with a single JSON object in a markdown block ```json...```.

```json
{
  "chart_type": "<the selected chart type>",
  "reasoning": "<explain why you chose this chart type>"
}
```

Example for a direct match:
```json
{
  "chart_type": "gantt",
  "reasoning": "The user explicitly asked for a Gantt chart."
}
```

Example for a suggestion:
```json
{
  "chart_type": "pie",
  "reasoning": "The 'sunburst' chart is not supported. A 'pie' chart is a good alternative for showing proportions."
}
```

Example for an unsupported request:
```json
{
  "chart_type": "ascii",
  "reasoning": "The user requested a 'sunburst' chart, which is not supported. Falling back to ASCII representation."
}
```
