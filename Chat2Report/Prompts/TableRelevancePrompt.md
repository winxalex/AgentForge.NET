﻿# 🧠 Table Relevance Analysis Prompt

### Instructions:

1.  Read the user's query carefully and translate it to English to analyze it.
2.  Examine the provided database schema, which includes tables, their descriptions.
3.  Based on the user's query and the schema, determine which tables are necessary to construct a SQL query that answers the question.
4.  If several tables contain similar data, include all of them as they might be in a master-detail relationship or otherwise linked.
5.  Provide a brief `reasoning` for your selection, explaining how the chosen tables relate to the user's query(In Macedonian language).
6.  Return your answer as a JSON object in a markdown block ```json...```. The object must contain two keys:
    *   `"reasoning"`: A string explaining your choices.
    *   `"relevant_tables"`: A JSON array of objects. Each object must have two keys: `"name"` (the fully qualified table name) and `"description"` (the table's description from the schema).
7.  **IMPORTANT**: Only include fully qualified table names (e.g., `schemaName.tableName`) in the `name` property of each object. Do not invent table names. If no tables are relevant, the `"relevant_tables"` array should be empty.
8.  Your entire response must be only the JSON object, in markdown block format ```json...```.

### Example Output:
```json
{
  "reasoning": "The user is asking about support cases and their status. The 'dbo.psPosts' table contains the details of each case, and 'dbo.psStatus' contains the descriptions for different statuses, which are linked by psStatus ID.",
  "relevant_tables": [
    {
      "name": "dbo.psPosts",
      "description": "Contains all user-submitted support cases (tickets), including details like subject, description, priority, and status."
    },
    {
      "name": "dbo.psStatus",
      "description": "A lookup table that defines the different statuses a support case can have (e.g., Open, Closed, In Progress)."
    }
  ]
}
```

If no tables are relevant, return an empty array inside the object:
```json
{
  "reasoning": "Explain why no tables are relevant.",
  "relevant_tables": []
}
```

🔄 NOW ANSWER THIS NEW CASE

### Available tables and description:

    | Table Name | Description |
    |------------|-------------|
    {{#each all_table_definitions}}
    | {{FullQualifiedTableName}} | {{Description}} | 
    {{/each}}


### User Query:

`{{{user_query}}}`

### Relevant Tables:
