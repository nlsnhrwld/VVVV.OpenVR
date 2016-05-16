﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;
using Valve.VR;
using VVVV.Utils.VMath;
using SlimDX;
using SlimDX.Direct3D11;
using System.Runtime.InteropServices;

namespace VVVV.Nodes.ValveOpenVR
{
    [PluginInfo(Name = "Poser", Category = "OpenVR", Tags = "vr, htc, vive, oculus, rift", Author = "tonfilm")]
    public class ValveOpenVRInputNode : IPluginEvaluate, IDisposable
    {
        [Output("System", Order = -100, IsSingle = true)]
        ISpread<CVRSystem> FSystemOut;

        [Output("View")]
        ISpread<Matrix> FViewOut;

        [Output("Projection")]
        ISpread<Matrix> FProjectionOut;

        [Output("Eye to Head")]
        ISpread<Matrix> FEyeToHeadOut;

        [Output("Render Poses")]
        ISpread<Matrix> FRenderPosesOut;

        [Output("Game Poses")]
        ISpread<Matrix> FGamePosesOut;

        [Output("Vertices Left")]
        ISpread<float> FVerticesLeftOut;

        [Output("Vertices Right")]
        ISpread<float> FVerticesRightOut;

        [Output("Device Class")]
        ISpread<string> FDeviceClassOut;

        [Output("Recommended Texture Size")]
        ISpread<Vector2D> FTexSizeOut;

        [Output("Error")]
        ISpread<String> FErrorOut;

        //the vr system
        CVRSystem FOpenVRSystem;

        void SetStatus(object toString)
        {
            if(toString is EVRInitError)
                FErrorOut[0] = OpenVR.GetStringForHmdError((EVRInitError)toString);
            else
                FErrorOut[0] = toString.ToString();
        }

        void InitOpenVR()
        {
            if (OpenVR.IsHmdPresent())
            {
                var initError = EVRInitError.Unknown;
                FOpenVRSystem = OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Scene);
                SetStatus(initError);
                if (initError != EVRInitError.None) return;

                //texture size
                uint sizeX = 0;
                uint sizeY = 0;
                FOpenVRSystem.GetRecommendedRenderTargetSize(ref sizeX, ref sizeY);
                FTexSizeOut[0] = new Vector2D(sizeX, sizeY);
                
            }
            else
            {
                SetStatus(EVRInitError.Init_HmdNotFound);
            }
        }

        void ShutDownOpenVR()
        {
            OpenVR.Shutdown();
            FOpenVRSystem = null;
        }

        public void Evaluate(int SpreadMax)
        {
            
            if (FOpenVRSystem == null)
                InitOpenVR();

            if(FOpenVRSystem != null)
            {
                //poses
                var poseCount = (int)OpenVR.k_unMaxTrackedDeviceCount;
                var renderPoses = new TrackedDevicePose_t[poseCount];
                var gamePoses = new TrackedDevicePose_t[poseCount];
                var error = OpenVR.Compositor.WaitGetPoses(renderPoses, gamePoses);
                SetStatus(error);
                if (error != EVRCompositorError.None) return;
                FRenderPosesOut.SliceCount = poseCount;
                FGamePosesOut.SliceCount = poseCount;
                FDeviceClassOut.SliceCount = poseCount;

                for (int i = 0; i < poseCount; i++)
                {
                    FRenderPosesOut[i] = renderPoses[i].mDeviceToAbsoluteTracking.ToMatrix();
                    FGamePosesOut[i] = gamePoses[i].mDeviceToAbsoluteTracking.ToMatrix();
                    FDeviceClassOut[i] = FOpenVRSystem.GetTrackedDeviceClass((uint)i).ToString();
                }

                //camera properties
                var projL = FOpenVRSystem.GetProjectionMatrix(EVREye.Eye_Left, 0.05f, 100, EGraphicsAPIConvention.API_DirectX);
                var projR = FOpenVRSystem.GetProjectionMatrix(EVREye.Eye_Right, 0.05f, 100, EGraphicsAPIConvention.API_DirectX);
                FProjectionOut.SliceCount = 2;
                FProjectionOut[0] = projL.ToProjectionMatrix();
                FProjectionOut[1] = projR.ToProjectionMatrix();

                var eyeL = FOpenVRSystem.GetEyeToHeadTransform(EVREye.Eye_Left).ToEyeMatrix();
                var eyeR = FOpenVRSystem.GetEyeToHeadTransform(EVREye.Eye_Right).ToEyeMatrix();
                FEyeToHeadOut.SliceCount = 2;
                FEyeToHeadOut[0] = eyeL;
                FEyeToHeadOut[1] = eyeR;


                //view
                FViewOut.SliceCount = 2;
                FViewOut[0] = Matrix.Invert(eyeL * FRenderPosesOut[0]);
                FViewOut[1] = Matrix.Invert(eyeR * FRenderPosesOut[0]);

                //hidden pixels mesh
                var meshLeft = FOpenVRSystem.GetHiddenAreaMesh(EVREye.Eye_Left);
                var meshRight = FOpenVRSystem.GetHiddenAreaMesh(EVREye.Eye_Right);
                GetMeshData(meshLeft, FVerticesLeftOut);
                GetMeshData(meshRight, FVerticesRightOut);

                FSystemOut[0] = FOpenVRSystem;
            }
        }

        void GetMeshData(HiddenAreaMesh_t meshData, ISpread<float> ret)
        {
            var floatCount = (int)meshData.unTriangleCount * 3 * 2;
            ret.SliceCount = floatCount;

            Marshal.Copy(meshData.pVertexData, ret.Stream.Buffer, 0, floatCount);
        }

        public void Dispose()
        {
            ShutDownOpenVR();
        }
    }
}
