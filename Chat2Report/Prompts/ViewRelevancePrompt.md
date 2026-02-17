# 🧠 View Relevance Analysis Prompt


## Context

You will be provided with:
1.  A list of database tables that have already been determined to be relevant to the query.
2.  A list of available database views and their business descriptions.

Your goal is to select views that provide **additional information** that is not available in the relevant tables but is required by the user's query.

## Instructions

1.  Carefully read the user's query to understand their intent.
2.  Review the list of **Relevant Tables**.
3.  Review the list of **Available Views** and their descriptions.
4.  Identify which views are **absolutely necessary** to supplement the data from the relevant tables to answer the user's query. Do not include views if the required information is already present in the relevant tables.
5.  Provide a brief `reasoning` for your selection, explaining why each view is necessary and what additional information it provides(In Macedonian language).
6.  Your final output must be a **JSON object** inside a markdown block ```json...```. The object must containwith two keys:
    *   `"reasoning"`: A string explaining your choices.
    *   `"relevant_views"`: A JSON array of objects. Each object must have two keys: `"name"` (the fully qualified view name) and `"description"` (the view's description from the context).
7.  Do not provide any explanation or other text outside of the JSON object. If no views are necessary, the `"relevant_views"` array should be empty.

### Example Output:
```json
{
  "reasoning": "The user's query requires aggregated monthly sales data, which is not directly available in the 'dbo.Orders' or 'dbo.OrderItems' tables. The 'dbo.MonthlySalesSummary' view provides this pre-calculated information, making it essential.",
  "relevant_views": [
    {
      "name": "dbo.MonthlySalesSummary",
      "description": "Provides a summary of total sales per month."
    }
  ]
}
```

If no views are relevant, return an empty array inside the object:
```json
{
  "reasoning": "Explain why no views are relevant.",
  "relevant_views": []
}
```

---

## Your Task

**User Query:**
> {{{user_query}}}


**Relevant Tables:**
    | Table Name | Description |
    |-------------|-------------|
    {{#each relevant_tables}}
    | {{name}} | {{description}} | 
    {{/each}}

**Available Views:**
    | View Name | Description |
    |-------------|-------------|
    {{#each all_view_definitions}}
    | {{FullQualifiedViewName}} | {{Description}} | 
    {{/each}}

**Relevant Views:**