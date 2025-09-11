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
        FreeBarber_Rejected_ToCustomer,
        Store_Rejected_ToCustomer,
        Store_Invite_FreeBarber,
        FreeBarber_Approved_ToCustomer,
        FreeBarber_Rejected_To_Store
    }
}
