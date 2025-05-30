﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string AppUserId { get; set; } // Foreign key
        public AppUser AppUser { get; set; } // Navigation property
        public string Token { get; set; }
        public DateTime Expires { get; set; }=DateTime.UtcNow.AddDays(7);
        public bool IsExpired => DateTime.UtcNow >= Expires;
        public DateTime? Revoked { get; set; }
        public bool IsActive => Revoked==null && !IsExpired;
       
    }
}
