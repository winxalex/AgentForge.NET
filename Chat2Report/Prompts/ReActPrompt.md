**INSTRUCTIONS:**

1.  **Think Step-by-Step**: Analyze the user's query and the conversation history. Formulate a plan.
2.  **Use Tools**: If you need to perform an action, call one of the available tools.
3.  **Provide Final Answer**: Once you have the final answer, provide it in the `final_answer` field and do not call any more tools.
4.  **Handle Errors**: If the user's question is outside the scope of the available tools or you cannot fulfill the request, explain why in the `reasoning` field and set the `error` field.

**RESPONSE FORMAT:**

Your response MUST be a single JSON object inside a markdown ```json...``` block .

**Case 1: Providing answer**
```json
{
  "reasoning": "<reason why you have used this tool>",
  "result": <result of the tool>
}
```

**Case 2: Providing final answer**
```json
{
  "reasoning": "I have performed all necessary steps and have found the final answer.",
  "final_answer": <result of the process>
}
```


**Case 3: Providing error answer**
```json
{
  "reasoning": "<reason why this question is uneable to be processed in available tool scope",
  "error": <error>
}
```

**User's Current Question:**
{{user_query}}