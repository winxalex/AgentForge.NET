# 🧠 Semantic Structuring Prompt



## 🎯 Your Task
1. Read the user's query carefully.
2. Carefully analyze the **ontology** to understand domain-specific **entities**, **attributes**, and their relationships.  
3. Interpret the **implicit meaning** of the user’s question using this ontology.  
4. Generate a **explicit version** of the original user query by making all implicit meaning explicit, in clearer and more formal language.  
5. Extract a list of structured **attributes and values** that can be used to construct precise SQL filters, selection or aggregation.

---

## 📚 Ontology

```json
{{{tables_ontology_map}}}
{{{views_ontology_map}}}
```


---
{{#if relevant_function_definitions}}
## 🔧 Available Functions:
{{#each relevant_function_definitions}}
	 {{{RawJsonDescription}}}
{{/each}}
{{/if}}

```json
{
  "user_query": "<original user query in Macedonian>",
  "explicit_user_query": "<formal and complete version of the query, in English, with all implicit meaning made explicit>",
  "extracted_attributes": [ /* structured attribute objects */ ]
}
```

---

## 🔍 `extracted_attributes` Object Definition

Each item in this list describes one extracted attribute and has the following properties:

- **`name`**: (string, English)  
  Name of the attribute  in `snake_case` format (e.g. `something1_something2`, `birth_date`, `ticket_id`,etc.)
- **`schema_references`: (array of strings, optional)
A list of all schema objects from the ontology that are required to define or compute this attribute.
These references can be:
direct database columns, e.g. "schema.table.column"
table-valued or scalar functions used to derive related values, e.g. "schema.functionName"
Usage rules:
For an attribute that maps directly to a single column, provide a single-element array:
`["schema.table.column"]`
For a computed attribute (e.g., calculation, concatenation, or attribute derived via a function), list all involved source columns and/or function names:
`["schema.table.column1", "schema.functionName", ...]`

Use an empty array [] only if the attribute is not derived from any database column or function.
  - **Always use this field.** Do not invent column names; use only those from the provided ontology.
- **`context`**: (string, English)  
  Always starts with **"Defines"**.  
  A generic, universal explanation of the attribute’s meaning. For computed attributes, explain how it's computed.
  Ideal for semantic matching with database column descriptions.
- **`intent`**: The `intent` field describes how the attribute should be used in the structured query.

      **Possible values and descriptions:**

      | intent      | Description                                 | Typical SQL Mapping              | Example                          |
      |-------------|---------------------------------------------|----------------------------------|----------------------------------|
      | `SELECT`    | Include in final output                     | `SELECT column`                  | Show `customer_name`             |
      | `FILTER`    | Restrict rows based on condition            | `WHERE column = value`           | Only where `status = 'active'`   |
      | `GROUP`     | Group rows by this attribute                | `GROUP BY column`                | Group by `region_id`             |
      | `ORDER`     | Sort rows based on this attribute           | `ORDER BY column ASC|DESC`      | Order by `created_date DESC`     |
      | `AGGREGATE` | Compute a metric over grouped data          | `COUNT(*)`, `SUM(column)`        | Count items per category         |
      | `LIMIT`     | Restrict the number of returned rows        | `LIMIT n` or `LIMIT n OFFSET m` | Top 10 products in SQLite |
      | `JOIN`      | Describes a join between two entities. ALWAYS include a JOIN attribute whenever an extracted attribute requires data from more than one table or function. `schema_references` MUST contain exactly the two columns that form the join condition (e.g., `["schema.table1.colA", "schema.table2.colB"]`). Do not skip JOIN attributes — they are mandatory whenever multiple sources are involved. | `JOIN table ON condition` | Join `tickets` with `users` |

- **`value`**: (object, optional)  
  Describes the filter value.
  - **`data`**: (array)  Original value or values(if there are more or if they represent range). Exact format as extracted from user input.
  - **`type`**: (string) One of:
    - `SingleEnum`, `EnumSet`
    - `SingleDate`, `DateRange` (when `data` is one explicit date, or range start and end are explicitly given as calendar dates)
    - `SingleText`, `TextSet`
    - `SingleNumeric`, `NumericRange`
    - `TemporalExpression` (when `data` contains relative temporal expressions like "last week", "next month", "past 30 days", etc.)`)
  - **`resolved`**: (optional,array)     
       Example: for temporal expressions (`TemporalExpression`): `[ "<resolved date>"]` or `[ "<resolved start date>", "<resolved end date>"]` depending of the nature of the expression.
  - **`original_user_text`**: (string)  Chunk of user query text containing this attribute
    Exact phrase from user input.
  - **`where_hint`**: (string, English, PascalCase, used only when intent is `FILTER`)  
    Suggests the SQL operator. **You MUST use one of the following `PascalCase` values:**
        *   For single values: `Equals`, `NotEquals`, `Greater`, `GreaterOrEqual`, `Less`, `LessOrEqual`.
        *   For ranges: `BetweenInclusive`, `BetweenExclusive`.
        *   For sets of values: `InList`, `NotInList`.
        *   For string matching: `Contains`, `StartsWith`, `EndsWith`, `Like` (for complex patterns), `NotLike`.
        *   For non applicable: `Unknown`.
  - 
  - **`antonyms`**: (array, optional) one for each value in `data` array. Known antonyms (e.g., `closed` for `not closed`)



---

## 📅 Date Resolution

Use the current date to resolve temporal expressions like:

- "this month", "last week"
- "past 30 days", "next year"

**Current date context**: `{{{current_datetime}}}`

---

## ✅ Example with Computed Attribute

### User Query (in Macedonian):
```
Прикажи го времето за решавање на сите затворени тикети
```

### JSON Output:
```json
{
  "user_query": "Прикажи го времето за решавање на сите затворени тикети",
  "explicit_user_query": "Show the resolution time for all closed tickets.",
  "extracted_attributes": [
    {
      "name": "ticket_status",
      "schema_references": ["dbo.StatusThreadAI.opis"],
      "context": "Defines the status or state of the ticket or case.",
      "intent": "FILTER",
      "value": {
        "data": ["Затворен"],
        "type": "SingleEnum",
        "original_user_text": "затворени",
        "where_hint": "Equals"
      }
    },
    {
      "name": "time_to_resolution",
      "schema_references": ["dbo.forumThreadsAI.DatumVremeZabelezan", "dbo.forumThreadsAI.DatumVremeResen"],
      "context": "Defines the time to resolution, computed as the difference between the resolution date and the creation date.",
      "intent": "SELECT",
      "value": null
    }
  ]
}
```
---

### ✅ User Query (in Macedonian):
```
{{{user_query}}}
```

### ✅ **Respond final  JSON in a markdown block ```json...``` and nothing else**:
