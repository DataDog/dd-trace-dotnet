<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Elasticsearch.aspx.cs" Inherits="Samples.WebForms.Empty.Elasticsearch" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Elasticsearch</title>
</head>
<body>
<form id="form1" runat="server">
    <h2>Elasticsearch</h2>
    <div><%: DateTime.Now %></div>
    <div>Profiler attached: <%: Samples.WebForms.Empty.NativeMethods.IsProfilerAttached() %></div>
</form>
</body>
</html>