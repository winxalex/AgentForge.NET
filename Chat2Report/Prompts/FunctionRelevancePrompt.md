# Function Relevance Determination

## Task
Based on the user's query and the provided context, identify which of the following database functions are relevant for answering the query.

## Context: About Inline Table-Valued Functions (iTVFs)
Inline table-valued functions (iTVFs) are a type of user-defined function in SQLite that return a table. They are essentially a "parameterized view" - a reusable database object that you can query like a regular table, but with the added benefit of accepting input parameters. Your goal is to identify when a user's query requires the specific logic encapsulated by one of these functions, especially when the available tables and views are insufficient on their own.

## Query Context
- **User Query**: `{{{user_query}}}`

**Relevant Tables:**
    | Table Name | Description |
    |-------------|-------------|
    {{#each relevant_tables}}
    | {{name}} | {{description}} | 
    {{/each}}

**Relevant Views:**
    | View Name | Description |
    |-------------|-------------|
    {{#each relevant_views}}
    | {{name}} | {{description}} | 
    {{/each}}

## Available Functions

	 | Function Signiture | Description | Typical Usage |
	 |--------------------|-------------|---------------|
	 {{#each all_function_definitions}}
	 | {{FunctionSignature}} | {{Description}} |{{TypicalUsage}} |
	 {{/each}}

## Instructions
1.  Based on the user's query, the already identified relevant tables/views, and the function descriptions, determine which functions are **additionally necessary** to construct a SQLite query that answers the question.
2.  A function is necessary if the relevant tables/views do not contain the required data, but the function can provide it (e.g., mapping a `CaseNo` to an `AppID`).
3.  Pay close attention to the `Typical Usage` column as it provides strong hints about when and how to use a function.
4.  Provide a brief `reasoning` for your selection, explaining why each function is necessary and what additional information it provides.
5.  Return your answer as a JSON object. The object must contain two keys:
    *   `"reasoning"`: A string explaining your choices.(In Macedonian language)
    *   `"relevant_functions"`: A JSON array of objects. Each object must have two keys: `"name"` (the fully qualified function name) and `"description"` (the function's description from the context).
6.  **IMPORTANT**: Only include fully qualified function names from the provided list. Do not invent function names. If no functions are relevant, the `"relevant_functions"` array should be empty.
7.  Your entire response must be only the JSON object, in markdown block format ```json...```.

### Example Output:
```json
{
  "reasoning": "The user's query requires mapping a CaseNo to an AppID to filter by application. The 'dbo.MapCaseNoToAppID' function provides this mapping, which is not available in the selected tables or views.",
  "relevant_functions": [
    {
      "name": "dbo.MapCaseNoToAppID",
      "description": "Maps a given CaseNo to its corresponding AppID."
    }
  ]
}
```

If no functions are relevant, return an empty array inside the object:
```json
{
  "reasoning": "Explain why no functions are relevant.",
  "relevant_functions": []
}
```

## Relevant Functions (JSON Object in a markdown block ```json...```):
