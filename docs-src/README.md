# Notes on in-code documentation for this project

All public types, methods, and properties should have documentation comments in the standard C# XML comment format. These will be automatically included in the [HTML documentation](https://launchdarkly.github.io/dotnet-eventsource) that is generated on release.

Non-public items may have documentation comments as well, since those may be helpful to other developers working on this project, but they will not be included in the HTML documentation.

The `docs-src` subdirectory contains additional Markdown content that is included in the documentation build, as follows:

* `index.md`: This text appears on the landing page of the documentation.
* `namespaces/<Fully.Qualified.Name.Of.Namespace>.md`: A file that is used as the description of a specific namespace. The first line is the summary, which will appear on both the landing page and the API page for the namespace; the rest of the file is the full description, which will appear on the API page for the namespace.

Markdown text can include hyperlinks to namespaces, types, etc. using the syntax `<xref:Fully.Qualified.Name.Of.Thing>`.
