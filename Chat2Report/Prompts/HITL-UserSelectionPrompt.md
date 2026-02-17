## Context
The user's query mentioned a name, but the system found multiple potential matches in the directory. You need to present these matches to the user so they can select the correct one.

The input contains a dictionary `ambiguous_user_mappings`. This dictionary has **only one entry**.
- The **value** is a list of `UserModel` objects, each representing a possible match. Each `UserModel` has `UserName`, `FullName`, and `Department`.
The original ambiguous name is also provided directly in the `original_ambiguous_name` variable.

## Your Task
Generate a JSON object representing a `DynamicHtmlContent`. This object must have two fields: `html` and `context`.

1.  **`html` (string)**:
    - Generate HTML that presents the choices to the user.
    - Start with a clear question, like "Кој корисник го мислевте за '{{{original_ambiguous_name}}}'?"
    - Create a dropdown list (`<select>`) with a class like `form-select user-selection-choices`.
    - Add a disabled, selected first option to serve as a placeholder (e.g., "Изберете корисник...").
    - For each user in the list, create an `<option>` element.
    - The option's text should clearly identify the user (e.g., FirstName and LastName and Department in Macedonian cyrilic as given in the input object).
    - **Crucially**, the `<select>` element must have a standard JavaScript `onchange` attribute that calls the global function `blazorInterop.invokeCSharp`.
    - This function must be called with a single JavaScript object containing two properties:
        - `methodName`: A string with the name of the C# method to invoke. For this specific task, you **must** use the value `"HandleChoiceClickAsync"`.
        - `parameters`: An array containing the parameters for the C# method. This array must contain **exactly two elements in this order**:
            1. The original ambiguous name (from the `original_ambiguous_name` variable).
            2. The value of the selected option, which should be the user's `FirstName` and `LastName`.
            Use the first and last names, in Macedonian cyrilic exactly as given in the input object. Don't change a bit.
            Do not translate.
            Do not transliterate into Latin letters.
            Do not change letters (e.g., „љ“ must remain „љ“ and not become „л“).
            Do not correct spelling.
    - ### Example of the `onchange` attribute on the `<select>` element:
    ```javascript
    onchange="if(this.value) { blazorInterop.invokeCSharp({ methodName: 'HandleChoiceClickAsync', parameters: ['{{{original_ambiguous_name}}}', this.value] }) }"
     ```

2.  **`context` (object)**:
    - Create a JSON object that contains the original ambiguous name.
    - The object should look like this: `{ "original_name": "{{{original_ambiguous_name}}}" }`


---

### Input Data
`ambiguous_user_mappings`: {{{toJSON ambiguous_user_mappings}}}

### Respond with the final JSON object in a markdown block format ```json...``` and nothing else.