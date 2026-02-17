﻿# 🧠 Domain Relevance Analysis and Ranking Prompt

## User Query
`{{{user_query}}}`

## Candidate Domains
Here is a list of potential domains in a markdown table. Each domain has a name and a description.

     | Domain Name | Description |
     |-------------|-------------|
     {{#each candidate_domains}}
     | {{Domain.Name}} | {{Domain.Description}} |
     {{/each}}

## Instructions
1.  Carefully read the user's query to understand their intent.
2.  Review the descriptions of all candidate domains in the markdown table.
3.  Decide which domains are relevant to the query. If a domain is not relevant, do not include it.
4.  Provide a brief `reasoning` (in Macedonian) for your selection and ranking, explaining how the chosen domains relate to the user's query.
5.  Return a JSON object with the following keys:
    *   `"reasoning"`: A string in Macedonian explaining your choices and ranking.  
    *   `"ranked_domains"`: A JSON array of objects, ordered from **most relevant to least relevant**. Each object must have two keys: `"name"` (the domain's name) and `"description"` (the domain's description).
    *   `"guidance"`: (Optional) A string in Macedonian that provides guidance to the user on how to reformulate their query if no relevant domains are found. This key should only be present when `"ranked_domains"` is empty.
6.  Only include `Name` and `Description` values from the provided table. Do not invent new ones.
7.  If no domain is relevant, the `"ranked_domains"` array must be empty. The `"reasoning"` should explain why (in Macedonian). The `"guidance"` key must be added to help the user. The guidance text should suggest specific, reformulated questions by combining the user's original query with the available domains. For example, if the query is "Покажи детали за 'ABC'" and the domains are "Customers" and "Products", a good guidance would be "Дали мислевте на: 'Клиент со име ABC' или 'Продукт со назив ABC'?".
8.  Your entire response must be only the JSON object in a markdown block ```json...```.

## Example Output

### Example with relevant domains:
```json
 {
  "reasoning": "Корисничкото прашање се однесува на 'тикети' и 'случаи', што директно се мапира на 'Helpdesk' доменот. 'Users' доменот е исто така релевантен бидејќи содржи кориснички информации кои може да бидат потребни за идентификација на подносителот на случајот.",
  "ranked_domains": [
    {
      "name": "Helpdesk",
      "description": "This domain defines all information related to user-submitted support cases, including their status, priority, department, submission date, and related communication threads."
    },
    {
      "name": "Users",
      "description": "This domain covers user accounts, permissions, roles, and security-related information."
    }
  ]
 }
```

### Example with no relevant domains:
```json
 {
  "reasoning": "Корисничкото прашање 'Покажи детали за ABC' е премногу нејасно. Не е јасно дали 'ABC' се однесува на клиент, продукт или нешто друго.",
  "ranked_domains": [],
  "guidance": "Ве молиме наведете поконкретно. Дали мислевте на: \n- Клиент со име 'ABC'? \n- Продукт со назив 'ABC'?"
 }
```