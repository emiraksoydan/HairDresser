using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities.Concrete.Enums
{
    /// <summary>
    /// Müşteri-FreeBarber randevusunda dükkan seçim tipi
    /// </summary>
    public enum StoreSelectionType
    {
        /// <summary>
        /// İsteğime Göre - Dükkan seçilmez, sadece müşteri ve free barber arasında randevu
        /// </summary>
        CustomRequest = 0,
        
        /// <summary>
        /// Dükkan Seç - Free barber dükkan seçecek, randevu notu ile birlikte
        /// </summary>
        StoreSelection = 1
    }
}

