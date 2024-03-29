﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="VectorIndexTypeNode.cs" company="DigitalGlobe">
//   Copyright 2015 DigitalGlobe
//   
//      Licensed under the Apache License, Version 2.0 (the "License");
//      you may not use this file except in compliance with the License.
//      You may obtain a copy of the License at
//   
//          http://www.apache.org/licenses/LICENSE-2.0
//   
//      Unless required by applicable law or agreed to in writing, software
//      distributed under the License is distributed on an "AS IS" BASIS,
//      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//      See the License for the specific language governing permissions and
//      limitations under the License.
// </copyright>
// <summary>
//   Defines the VectorIndexTypeNode type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Gbdx.Vector_Index
{
    using System.Windows.Forms;

    /// <summary>
    /// The vector index type node.
    /// </summary>
    public class VectorIndexTypeNode : TreeNode
    {
        /// <summary>
        /// Gets or sets the response object.
        /// </summary>
        public SourceTypeResponseObject ResponseObject { get; set; }

        /// <summary>
        /// Gets or sets the geometry.
        /// </summary>
        public SourceType Geometry { get; set; }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        public SourceType Source { get; set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public SourceType Type { get; set; }
    }
}