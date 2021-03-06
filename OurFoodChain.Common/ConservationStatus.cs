﻿using System;
using System.Collections.Generic;
using System.Text;

namespace OurFoodChain.Common {

    public class ConservationStatus :
       IConservationStatus {

        public bool IsExinct => Date.HasValue;
        public DateTimeOffset? Date { get; set; }
        public string Reason { get; set; }

    }

}