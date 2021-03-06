﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MeshGeometryModel3D.cs" company="Helix Toolkit">
//   Copyright (c) 2014 Helix Toolkit contributors
// </copyright>
// <summary>
//
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Windows;

namespace HelixToolkit.Wpf.SharpDX
{
    using System;
    using System.ComponentModel;
    using System.Linq;

    using global::SharpDX;

    using global::SharpDX.Direct3D;

    using global::SharpDX.Direct3D11;

    using global::SharpDX.DXGI;

    using Buffer = global::SharpDX.Direct3D11.Buffer;
    using System.Runtime.CompilerServices;
    using System.Diagnostics;
    using System.Collections.Generic;

    public class MeshGeometryModel3D : MaterialGeometryModel3D
    {
        public static readonly DependencyProperty FrontCounterClockwiseProperty = DependencyProperty.Register("FrontCounterClockwise", typeof(bool), typeof(MeshGeometryModel3D), new PropertyMetadata(true, RasterStateChanged));

        public bool FrontCounterClockwise
        {
            set
            {
                SetValue(FrontCounterClockwiseProperty, value);
            }
            get
            {
                return (bool)GetValue(FrontCounterClockwiseProperty);
            }
        }

        public static readonly DependencyProperty CullModeProperty = DependencyProperty.Register("CullMode", typeof(CullMode), typeof(MeshGeometryModel3D), new PropertyMetadata(CullMode.None, RasterStateChanged));

        public CullMode CullMode
        {
            set
            {
                SetValue(CullModeProperty, value);
            }
            get
            {
                return (CullMode)GetValue(CullModeProperty);
            }
        }

        public static readonly DependencyProperty IsDepthClipEnabledProperty = DependencyProperty.Register("IsDepthClipEnabled", typeof(bool), typeof(MeshGeometryModel3D), new PropertyMetadata(true, RasterStateChanged));

        public bool IsDepthClipEnabled
        {
            set
            {
                SetValue(IsDepthClipEnabledProperty, value);
            }
            get
            {
                return (bool)GetValue(IsDepthClipEnabledProperty);
            }
        }

        public override int VertexSizeInBytes
        {
            get
            {
                return DefaultVertex.SizeInBytes;
            }
        }

        private DefaultVertex[] vertexArrayBuffer = null;

        protected override void OnRasterStateChanged()
        {
            Disposer.RemoveAndDispose(ref this.rasterState);
            if (!IsAttached) { return; }
            // --- set up rasterizer states
            var rasterStateDesc = new RasterizerStateDescription()
            {
                FillMode = FillMode,
                CullMode = CullMode,
                DepthBias = DepthBias,
                DepthBiasClamp = -1000,
                SlopeScaledDepthBias = +0,
                IsDepthClipEnabled = IsDepthClipEnabled,
                IsFrontCounterClockwise = FrontCounterClockwise,

                IsMultisampleEnabled = IsMultisampleEnabled,
                //IsAntialiasedLineEnabled = true,                    
                IsScissorEnabled = IsThrowingShadow? false : IsScissorEnabled,
            };
            try
            {
                this.rasterState = new RasterizerState(this.Device, rasterStateDesc);
            }
            catch (System.Exception)
            {
            }
        }

        protected override void OnGeometryChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnGeometryChanged(e);
        }

        protected override void OnGeometryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnGeometryPropertyChanged(sender, e);
            if (sender is MeshGeometry3D)
            {
                if (e.PropertyName.Equals(nameof(MeshGeometry3D.TextureCoordinates)))
                {
                    OnUpdateVertexBuffer(UpdateTextureOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Positions)))
                {
                    OnUpdateVertexBuffer(UpdatePositionOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Colors)))
                {
                    OnUpdateVertexBuffer(UpdateColorsOnly);
                }
                else if (e.PropertyName.Equals(nameof(MeshGeometry3D.Indices)) || e.PropertyName.Equals(Geometry3D.TriangleBuffer))
                {
                    Disposer.RemoveAndDispose(ref this.indexBuffer);
                    this.indexBuffer = Device.CreateBuffer(BindFlags.IndexBuffer, sizeof(int), this.Geometry.Indices.Array);
                    InvalidateRender();
                }
                else if (e.PropertyName.Equals(Geometry3D.VertexBuffer))
                {
                    OnUpdateVertexBuffer(CreateDefaultVertexArray);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        protected override bool OnAttach(IRenderHost host)
        {
            // --- attach
            if (!base.OnAttach(host))
            {
                return false;
            }

            // --- get variables
            this.vertexLayout = renderHost.EffectsManager.GetLayout(this.renderTechnique);
            this.effectTechnique = effect.GetTechniqueByName(this.renderTechnique.Name);

            // --- transformations
            this.effectTransforms = new EffectTransformVariables(this.effect);

            // --- material 
            this.AttachMaterial();

            // --- scale texcoords
            var texScale = TextureCoodScale;

            // --- get geometry
            var geometry = this.Geometry as MeshGeometry3D;

            // -- set geometry if given
            if (geometry != null)
            {
                //throw new HelixToolkitException("Geometry not found!");                

                // --- init vertex buffer
                CreateVertexBuffer(CreateDefaultVertexArray);
                Disposer.RemoveAndDispose(ref this.indexBuffer);
                // --- init index buffer
                this.indexBuffer = Device.CreateBuffer(BindFlags.IndexBuffer, sizeof(int), this.Geometry.Indices.Array);
            }
            else
            {
                throw new System.Exception("Geometry must not be null");
            }

            // --- init instances buffer            
            this.bHasInstances = this.effect.GetVariableByName("bHasInstances").AsScalar();
            // --- flush
            //this.Device.ImmediateContext.Flush();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OnUpdateVertexBuffer(Func<DefaultVertex[]> updateFunction)
        {
            CreateVertexBuffer(updateFunction);
            InvalidateRender();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CreateVertexBuffer(Func<DefaultVertex[]> updateFunction)
        {
            // --- get geometry
            var geometry = this.Geometry as MeshGeometry3D;

            // -- set geometry if given
            if (geometry != null && geometry.Positions != null)
            {
                Disposer.RemoveAndDispose(ref this.vertexBuffer);
                var data = updateFunction();
                if (data != null)
                {
                    this.vertexBuffer = Device.CreateBuffer(BindFlags.VertexBuffer, VertexSizeInBytes, data, geometry.Positions.Count);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDetach()
        {
            Disposer.RemoveAndDispose(ref this.vertexBuffer);
            Disposer.RemoveAndDispose(ref this.indexBuffer);
            Disposer.RemoveAndDispose(ref this.instanceBuffer);
            Disposer.RemoveAndDispose(ref this.effectMaterial);
            Disposer.RemoveAndDispose(ref this.effectTransforms);
            Disposer.RemoveAndDispose(ref this.bHasInstances);

            this.renderTechnique = null;
            this.effectTechnique = null;
            this.vertexLayout = null;

            base.OnDetach();
        }
        
        protected override bool CanRender(RenderContext context)
        {
            return base.CanRender(context) && this.effectMaterial != null;
        }
        
        protected override void OnRender(RenderContext renderContext)
        {
            // --- set constant paramerers             
            var worldMatrix = this.modelMatrix * renderContext.worldMatrix;
            this.effectTransforms.mWorld.SetMatrix(ref worldMatrix);

            // --- check shadowmaps
            this.hasShadowMap = this.renderHost.IsShadowMapEnabled;
            this.effectMaterial.bHasShadowMapVariable.Set(this.hasShadowMap);

            // --- set material params      
            this.effectMaterial.AttachMaterial();

            this.bHasInstances.Set(this.hasInstances);
            // --- set context
            renderContext.DeviceContext.InputAssembler.InputLayout = this.vertexLayout;
            renderContext.DeviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            renderContext.DeviceContext.InputAssembler.SetIndexBuffer(this.indexBuffer, Format.R32_UInt, 0);

            // --- set rasterstate            
            renderContext.DeviceContext.Rasterizer.State = this.rasterState;
            if (this.hasInstances)
            {
                // --- update instance buffer
                if (this.isInstanceChanged)
                {
                    if (instanceBuffer == null || instanceBuffer.Description.SizeInBytes < Matrix.SizeInBytes * this.Instances.Count)
                    {
                        Disposer.RemoveAndDispose(ref instanceBuffer);
                        this.instanceBuffer = Buffer.Create(this.Device, this.Instances.ToArray(), 
                            new BufferDescription(Matrix.SizeInBytes * this.Instances.Count, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0));
                    }
                    else
                    {
                        DataStream stream;
                        renderContext.DeviceContext.MapSubresource(this.instanceBuffer, MapMode.WriteDiscard, global::SharpDX.Direct3D11.MapFlags.None, out stream);
                        stream.Position = 0;
                        stream.WriteRange(this.Instances.ToArray(), 0, this.Instances.Count);
                        renderContext.DeviceContext.UnmapSubresource(this.instanceBuffer, 0);
                        stream.Dispose();
                    }
                    this.isInstanceChanged = false;
                }

                // --- INSTANCING: need to set 2 buffers            
                renderContext.DeviceContext.InputAssembler.SetVertexBuffers(0, new[]
                {
                    new VertexBufferBinding(this.vertexBuffer, VertexSizeInBytes, 0),
                    new VertexBufferBinding(this.instanceBuffer, Matrix.SizeInBytes, 0),
                });

                // --- render the geometry
                this.effectTechnique.GetPassByIndex(0).Apply(renderContext.DeviceContext);
                // --- draw
                renderContext.DeviceContext.DrawIndexedInstanced(this.Geometry.Indices.Count, this.Instances.Count, 0, 0, 0);
                this.bHasInstances.Set(false);
            }
            else
            {
                // --- bind buffer                
                renderContext.DeviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(this.vertexBuffer, VertexSizeInBytes, 0));
                // --- render the geometry
                // 
                var pass = this.effectTechnique.GetPassByIndex(0);
                pass.Apply(renderContext.DeviceContext);
                // --- draw
                renderContext.DeviceContext.DrawIndexed(this.Geometry.Indices.Count, 0, 0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Dispose()
        {
            this.Detach();
        }

        /// <summary>
        /// Creates a <see cref="T:DefaultVertex[]"/>.
        /// </summary>
        private DefaultVertex[] CreateDefaultVertexArray()
        {
            var geometry = (MeshGeometry3D)this.Geometry;
            var colors = geometry.Colors != null ? geometry.Colors.Array : null;
            var textureCoordinates = geometry.TextureCoordinates != null ? geometry.TextureCoordinates.Array : null;
            var texScale = this.TextureCoodScale;
            var normals = geometry.Normals != null ? geometry.Normals.Array : null;
            var tangents = geometry.Tangents != null ? geometry.Tangents.Array : null;
            var bitangents = geometry.BiTangents != null ? geometry.BiTangents.Array : null;
            var positions = geometry.Positions.Array;
            var vertexCount = geometry.Positions.Count;
            if (!ReuseVertexArrayBuffer || vertexArrayBuffer == null || vertexArrayBuffer.Length < vertexCount)
                vertexArrayBuffer = new DefaultVertex[vertexCount];

            for (var i = 0; i < vertexCount; i++)
            {
                vertexArrayBuffer[i].Position = new Vector4(positions[i], 1f);
                vertexArrayBuffer[i].Color = colors != null ? colors[i] : Color4.White;
                vertexArrayBuffer[i].TexCoord = textureCoordinates != null ? texScale * textureCoordinates[i] : Vector2.Zero;
                vertexArrayBuffer[i].Normal = normals != null ? normals[i] : Vector3.Zero;
                vertexArrayBuffer[i].Tangent = tangents != null ? tangents[i] : Vector3.Zero;
                vertexArrayBuffer[i].BiTangent = bitangents != null ? bitangents[i] : Vector3.Zero;
            }

            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdateTextureOnly()
        {
            var geometry = (MeshGeometry3D)this.Geometry;
            var vertexCount = geometry.Positions.Count;
            if (vertexArrayBuffer != null && geometry.TextureCoordinates != null && vertexArrayBuffer.Length >= vertexCount)
            {
                if (geometry.TextureCoordinates != null && geometry.TextureCoordinates.Count == vertexCount)
                {
                    var texScale = this.TextureCoodScale;
                    for (int i = 0; i < vertexCount; ++i)
                    {
                        vertexArrayBuffer[i].TexCoord = texScale * geometry.TextureCoordinates[i];
                    }
                }
                else
                {
                    for (int i = 0; i < vertexCount; ++i)
                    {
                        vertexArrayBuffer[i].TexCoord = Vector2.Zero;
                    }
                }
            }
            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdatePositionOnly()
        {
            var geometry = (MeshGeometry3D)this.Geometry;
            var vertexCount = geometry.Positions.Count;
            if (vertexArrayBuffer != null && vertexArrayBuffer.Length >= vertexCount)
            {
                var positions = geometry.Positions.Array;
                var normals = geometry.Normals != null ? geometry.Normals.Array : null;
                var tangents = geometry.Tangents != null ? geometry.Tangents.Array : null;
                var bitangents = geometry.BiTangents != null ? geometry.BiTangents.Array : null;
                for (int i = 0; i < vertexCount; ++i)
                {
                    vertexArrayBuffer[i].Position = new Vector4(positions[i], 1f);
                    vertexArrayBuffer[i].Normal = normals != null ? normals[i] : Vector3.Zero;
                    vertexArrayBuffer[i].Tangent = tangents != null ? tangents[i] : Vector3.Zero;
                    vertexArrayBuffer[i].BiTangent = bitangents != null ? bitangents[i] : Vector3.Zero;
                }
            }
            return vertexArrayBuffer;
        }

        private DefaultVertex[] UpdateColorsOnly()
        {
            var geometry = (MeshGeometry3D)this.Geometry;
            var vertexCount = geometry.Positions.Count;
            if (vertexArrayBuffer != null && geometry.Colors != null && vertexArrayBuffer.Length >= vertexCount)
            {
                if (geometry.Colors != null && geometry.Colors.Count == vertexCount)
                {
                    for (int i = 0; i < vertexCount; ++i)
                    {
                        vertexArrayBuffer[i].Color = geometry.Colors[i];
                    }
                }
                else
                {
                    for (int i = 0; i < vertexCount; ++i)
                    {
                        vertexArrayBuffer[i].Color = Color4.White;
                    }
                }
            }
            return vertexArrayBuffer;
        }
    }
}
