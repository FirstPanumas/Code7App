using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;
using Code7App.Models;  
namespace Code7App.Messages
{
    public class OrderCartMessage : ValueChangedMessage<List<CartItemModel>>
    {
        public OrderCartMessage(List<CartItemModel> cartItems) : base(cartItems)
        {
        }
    }
}
