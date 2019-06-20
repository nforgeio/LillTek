//-----------------------------------------------------------------------------
// FILE:        PostalAddress.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2015 by Jeffrey Lill.  All rights reserved.
// DESCRIPTION: Used to describe a physical location as a postal address.

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;

// $todo(jeff.lill): This is somewhat hardcoded for the United States at this point.

namespace LillTek.Common
{
    /// <summary>
    /// Used to describe a physical location as a postal address.
    /// </summary>
    /// <remarks>
    /// <note>
    /// This class essentially mimics the Bing Maps <b>CivicAddress</b> class.
    /// </note>
    /// </remarks>
    public class PostalAddress
    {
        //---------------------------------------------------------------------
        // Static members

        private static PostalAddress unknown = new PostalAddress();

        /// <summary>
        /// Returns an address with no address data.
        /// </summary>
        public static PostalAddress Unknown
        {
            get { return unknown; }
        }

        //---------------------------------------------------------------------
        // Instance members

        private const string UnknownModifyMsg = "Error: Attempt to modify the static Unknown address.";

        private string      addressLine1;
        private string      addressLine2;
        private string      city;
        private string      countryRegion;
        private string      building;
        private string      floorLevel;
        private string      stateProvince;
        private string      postalCode;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PostalAddress()
        {
            this.IsUnknown = true;
        }

        /// <summary>
        /// Indicates whether the instance contains any address data.
        /// </summary>
        public bool IsUnknown { get; private set; }

        /// <summary>
        /// The first line of the street address.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string AddressLine1
        {
            get { return addressLine1; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                addressLine1 = value;
            }
        }

        /// <summary>
        /// The second line of the streed address.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string AddressLine2
        {
            get { return addressLine2; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                addressLine2 = value;
            }
        }

        /// <summary>
        /// The city name.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string City
        {
            get { return city; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                city = value;
            }
        }

        /// <summary>
        /// The country or region name.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string CountryRegion
        {
            get { return countryRegion; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                countryRegion = value;
            }
        }

        /// <summary>
        /// The building.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string Building
        {
            get { return building; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                building = value;
            }
        }

        /// <summary>
        /// The floor.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string FloorLevel
        {
            get { return floorLevel; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                floorLevel = value;
            }
        }

        /// <summary>
        /// The state or province.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string StateProvince
        {
            get { return stateProvince; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                stateProvince = value;
            }
        }

        /// <summary>
        /// The postal code.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to modify the static <see cref="Unknown" /> address instance.</exception>
        public string PostalCode
        {
            get { return postalCode; }

            set
            {
                if (object.ReferenceEquals(this, Unknown))
                    throw new InvalidOperationException(UnknownModifyMsg);

                postalCode = value;
            }
        }
    }
}
