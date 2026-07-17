# Kiota Client Generation

Use this workflow when you need to create or refresh a Kiota-generated client library from an OpenAPI or Swagger definition.

## Goal
Generate a client project that can be updated later from the same Swagger source without manually rewriting the generated code.

## Workflow
1. Create a new .NET class library project for the client.
2. Add the necessary Kiota package references.
3. Add the project to the solution.
4. Generate the client from the Swagger/OpenAPI file into a project-local folder.
5. Save a repeatable update script and, when available, the Kiota lock file.
6. Build the project to verify the generated client compiles.

## Typical decisions
- Use a project-local folder such as `Client/` rather than a generic `Generated/` folder for cleaner layout.
- Prefer a PowerShell update script so the client can be refreshed later with one command.
- If the Swagger file does not define a server URL, note that the generated client needs a base URL set manually at runtime.

## Completion checks
- The client project builds successfully.
- The generated code exists in the project-local client folder.
- The update script can run again and refresh the client from the Swagger source.
