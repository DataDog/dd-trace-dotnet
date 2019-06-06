<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Samples.WebForms.Empty.Default" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Home</title>
</head>
<body>
<form id="form1" runat="server">
    <h2>Home</h2>
    <div><%: DateTime.Now %></div>
    <div>Profiler attached: <%: Samples.WebForms.Empty.NativeMethods.IsProfilerAttached() %></div>
</form>
</body>
</html>