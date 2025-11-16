using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    public enum NotificationType
    {
        Customer_To_Store_Requested,
        Customer_To_FreeBarber_Requested,
        FreeBarber_To_Store_Requested,
        Store_To_FreeBarber_Requested,
        Store_Rejected_ToCustomer,
        FreeBarber_Rejected_ToCustomer,
        FreeBarber_Approved_ToCustomer,
        FreeBarber_Approved_ToStore,
        Store_Approved_ToFreeBarber,
        Store_Approved_ToCustomer,


    }
}
