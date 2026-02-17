# 🧾 Person Name Extraction Prompt (Macedonian NER)

## Instructions

1.  **Focus only on names of people.** Ignore names of companies, departments, products, or other entities.  
2.  For each person found, create a JSON object and add it to the `extracted_persons` array.  
3.  **Field names must be exactly:**  
    - `first_name`  
    - `last_name`  
    (snake_case, lowercase, no other variation).  
4.  If a full name is found (e.g., "Марко Петров"), populate both `first_name` and `last_name`.  
5.  If only a first name or a last name is found, include only that field. Do **not** include the other field and do **not** use `null` values.  
6.  If multiple people are mentioned, create a separate JSON object for each one.  
7.  If no person names are found, return an empty array: `{ "extracted_persons": [] }`.  
8.  Your response MUST be **only a JSON object** in a markdown block ```json...```, without any extra text, explanations.  
9.  **Macedonian Language Hint:** For Macedonian names, last names often end in suffixes like "-ски", "-ска", "-иќ", "-ев", "-ов", "-ва". Use this as a strong hint to differentiate last names, but always verify with the context.  
10. Analyze the context carefully to avoid misinterpreting random words as names.  

---

## OUTPUT JSON STRUCTURE

```json
{
   "reasoning":"<why I've selected this enitites>(In Macedonian language)",
  "extracted_persons": [
    {
      "first_name": "<first name of person 1>",
      "last_name": "<last name of person 1>"
    },
    {
      "first_name": "<first name of person 2>"
    },
    {
      "last_name": "<last name of person 3>"
    }
  ]
}
```

---

## Examples

**User Query:** "Пријавата од Марко Петров е во тек, но чекаме и одговор од Ана."  
**Output:**  
```json
{
  "reasoning":"",
  "extracted_persons": [
    {
      "first_name": "Марко",
      "last_name": "Петров"
    },
    {
      "first_name": "Ана"
    }
  ]
}
```

**User Query:** "Проблемот со печатачот го пријави господинот Трајковски. Не е поврзано со серверот."  
**Output:**  
```json
{
  "reasoning":"",
  "extracted_persons": [
    {
      "last_name": "Трајковски"
    }
  ]
}
```

---

## TASK

Analyze the following user query and extract all person names.  

**User Query:**  
`{{{user_query}}}`
