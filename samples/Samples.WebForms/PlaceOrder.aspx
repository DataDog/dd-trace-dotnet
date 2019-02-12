<%@ Page Title="Order" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeFile="PlaceOrder.aspx.cs" Inherits="Samples.WebForms.PlaceOrder" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Order Placed</h2>
    <h3>Your order was placed.</h3>
    
    <asp:FormView ID="orderInfo" runat="server" ItemType="Samples.WebForms.Order" SelectMethod ="PlaceSingleItemOrder" RenderOuterTable="false">
        <ItemTemplate>
            <div>
                <h1><%#:Item.Id %></h1>
            </div>
            <br />
            <table>
                <tr>
                    <td style="vertical-align: top; text-align:left;">
                        <b>Type:</b><br /><%#:Item.Status %>
                        <br />
                    </td>
                </tr>
            </table>
        </ItemTemplate>
    </asp:FormView>
</asp:Content>
