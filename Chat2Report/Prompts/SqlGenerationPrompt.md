**Instructions:**
1.  **Your response MUST be a single JSON object with two keys: `sql` and `reasoning` inside markdown ```json...``` block. The value of `sql` must be the SQLite query as a string, and `reasoning` should briefly explain how you constructed the query.**
2.  Do not include any explanations, markdown formatting, or other text outside of the JSON object.
3.  Use the provided table and column names exactly as they appear in the schema information.
4.  Pay close attention to the suggested JOIN paths and WHERE conditions.
5.  If the user's request does not specify a limit on the number of records, add a `LIMIT 10` clause to the query to limit the results to 10 records.
6.  If the user's request does not specify selection columns, add a `*` to select all of the root table.
7.  The final output must be a single, executable SQL statement.
8. **When using columns in functions (e.g., aggregate functions like `SUM`, `AVG`, or date functions like `DATEDIFF`), always wrap those columns with `ISNULL()` or `COALESCE()` to prevent errors when values are NULL.**
9. **Whenever a column is used in filtering (`WHERE`), grouping (`GROUP BY`), or as input to functions, you must add an explicit `IS NOT NULL` check for that column.**
   - Example:  
     - `WHERE Column IS NOT NULL AND Column <> ''` before grouping.  
     - `WHERE T1.StartDate IS NOT NULL AND T1.EndDate IS NOT NULL` before `DATEDIFF`.  
10. **For all JOIN conditions, ensure both sides of the join columns are checked for `IS NOT NULL` before joining.**
    - Example:  
      - `JOIN TableB B ON A.Col = B.Col`  
      - Plus: `WHERE A.Col IS NOT NULL AND B.Col IS NOT NULL`

**Example Response Format:**
```json
{
  "sql": "SELECT * FROM MyTable WHERE Status = 'Active' LIMIT 10;",
  "reasoning": "<Explain how you created this SQL query>(In Macedonian language)"
}
```

---

{{{context}}}

---

### **User's Resolved Query**
For the user's request: `{{{resolved_user_query}}}`

### **SQL Query:**
