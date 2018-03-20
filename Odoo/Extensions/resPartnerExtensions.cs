using Odoo.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Odoo.Extensions
{
    public static class resPartnerExtensions
    {
        public static bool HasAddress(this resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Street)
                || !string.IsNullOrEmpty(partner.StreetNumber)
                || !string.IsNullOrEmpty(partner.Zip)
                || !string.IsNullOrEmpty(partner.City));
        }

        public static bool HasEmail(this resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Email));
        }

        public static bool HasPhone(this resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Phone));
        }

        public static bool HasMobile(this resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Mobile));
        }

        public static bool HasFax(this resPartner partner)
        {
            return (!string.IsNullOrEmpty(partner.Fax));
        }

    }
}
