**Instructions:**
1.  **Your response MUST be a single JSON object with one key: `sql`. The value of this key must be the SQLite query as a string.**
2.  Do not include any explanations or other text outside of the code block.
3.  Preserve the original intent of the query as much as possible.
4.  Use the provided context to understand the database schema and table relationships.
5.  Pay attention to the error message to identify and resolve the issue.
6.  The final output must be a single, executable SQL statement inside the code block.

---

**Example Response Format:**
```json
{
  "sql": "<fixed sql query>",
  "reasoning":"<Why and what you have fixed?(In Macedonian language)>"
}
```

### Database Schema and Analysis
{{{context}}}

---

### User's Request
For the user's request: `{{{original_user_query}}}`

### Invalid SQL and Error
The following SQL query: 
```sql
{{{cleaned_sql}}}
```
produced this error: `{{{error}}}`

### Corrected SQL Query:
