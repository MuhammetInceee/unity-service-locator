using System;
using UnityEngine;

namespace Attributes
{
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]

    public class IDAttribute : PropertyAttribute
    {
    }
}