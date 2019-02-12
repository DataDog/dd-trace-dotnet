<%@ Page Title="" Language="C#" MasterPageFile="~/Site.master" AutoEventWireup="true" CodeFile="ProductDetails.aspx.cs" Inherits="Samples.WebForms.ProductDetails" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="Server">
    <asp:FormView ID="productDetail" runat="server" ItemType="Samples.WebForms.Product" SelectMethod ="GetProduct" RenderOuterTable="false">
        <ItemTemplate>
            <div>
                <h1><%#:Item.Name %></h1>
            </div>
            <br />
            <table>
                <tr>
                    <td style="vertical-align: top; text-align:left;">
                        <b>Type:</b><br /><%#:Item.ProductType %>
                        <br />
                        <span><b>Price:</b>&nbsp;<%#: String.Format("{0:c}", Item.UnitCost) %></span>
                        <br />
                        <span><b>Product Number:</b>&nbsp;<%#:Item.Id %></span>
                        <br />
                    </td>
                </tr>
            </table>
        </ItemTemplate>
    </asp:FormView>
</asp:Content>
