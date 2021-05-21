﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using YagihataItems.YagiUtils;

namespace YagihataItems.RadialInventorySystemV3
{
    public static class RISGimmickBuilder
    {
        private static Texture2D boxIcon = null;
        private static Texture2D groupIcon = null;
        private static Texture2D reloadIcon = null;
        private static Texture2D menuIcon = null;
        public static void RemoveFromAvatar(RISVariables variables)
        {
            var avatar = variables.AvatarRoot;
            var autoGeneratedFolder = RISV3.AutoGeneratedFolderPath + variables.FolderID + "/";
            UnityUtils.DeleteFolder(autoGeneratedFolder + "Animations/");
            UnityUtils.DeleteFolder(autoGeneratedFolder + "SubMenus/");
            var fxLayer = avatar.GetFXLayer(autoGeneratedFolder + "AutoGeneratedFXLayer.controller", false);
            if(fxLayer != null)
            {
                foreach (var name in fxLayer.layers.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                    fxLayer.TryRemoveLayer(name);
                foreach (var name in fxLayer.parameters.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                    fxLayer.TryRemoveParameter(name);
            }
            if (avatar.expressionsMenu != null)
                avatar.expressionsMenu.controls.RemoveAll(n => n.name == "Radiai Inventory");
            if (avatar.expressionParameters != null)
                foreach (var name in avatar.expressionParameters.parameters.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                    avatar.expressionParameters.TryRemoveParameter(name);

        }
        public static void ApplyToAvatar(RISVariables variables)
        {
            if (boxIcon == null)
                boxIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(RISV3.WorkFolderPath + "Textures/box_icon.png");
            if (groupIcon == null)
                groupIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(RISV3.WorkFolderPath + "Textures/group_icon.png");
            if (reloadIcon == null)
                reloadIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(RISV3.WorkFolderPath + "Textures/reload_icon.png");
            if (menuIcon == null)
                menuIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(RISV3.WorkFolderPath + "Textures/ris_icon.png");

            var autoGeneratedFolder = RISV3.AutoGeneratedFolderPath + variables.FolderID + "/";
            if (!AssetDatabase.IsValidFolder(autoGeneratedFolder))
                UnityUtils.CreateFolderRecursively(autoGeneratedFolder);
            BuildExpressionParameters(variables, autoGeneratedFolder);
            BuildExpressionsMenu(variables, autoGeneratedFolder);
            BuildFXLayer(variables, autoGeneratedFolder);

            if(variables.ApplyEnabled)
            {
                foreach(var group in variables.Groups)
                {
                    foreach(var prop in group.Props)
                    {
                        if (variables.MenuMode == RISV3.RISMode.Simple)
                        {
                            prop.TargetObject.SetActive(prop.IsDefaultEnabled);
                            EditorUtility.SetDirty(prop.TargetObject);
                        }
                        else if (variables.MenuMode == RISV3.RISMode.Advanced && prop.MaterialOverride == null)
                        {
                            foreach (var subProp in prop.TargetObjects)
                            {
                                subProp.SetActive(prop.IsDefaultEnabled);
                                EditorUtility.SetDirty(subProp);
                            }
                        }
                    }
                }
            }
            EditorUtility.DisplayDialog("Radial Inventory System", "ビルド成功！", "OK");
        }

        private static void BuildFXLayer(RISVariables variables, string autoGeneratedFolder)
        {
            var avatar = variables.AvatarRoot;
            var fxLayer = avatar.GetFXLayer(autoGeneratedFolder + "AutoGeneratedFXLayer.controller");
            var animationsFolder = autoGeneratedFolder + "Animations/";
            UnityUtils.ReCreateFolder(animationsFolder);
            foreach (var name in fxLayer.layers.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                fxLayer.TryRemoveLayer(name);
            foreach (var name in fxLayer.parameters.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                fxLayer.TryRemoveParameter(name);
            foreach (var groupIndex in Enumerable.Range(0, variables.Groups.Count))
            {
                var group = variables.Groups[groupIndex];
                var groupName = group.GroupName;
                if (string.IsNullOrWhiteSpace(groupName))
                    groupName = "Group" + groupIndex.ToString();
                foreach (var propIndex in Enumerable.Range(0, group.Props.Count))
                {
                    var prop = group.Props[propIndex];
                    var propName = prop.GetPropName();
                    if (string.IsNullOrWhiteSpace(propName))
                        propName = "Prop" + propIndex.ToString();
                    var layerName = $"RISV3-MAIN-G{groupIndex}P{propIndex}";

                    var paramName = $"RISV3-G{groupIndex}P{propIndex}";
                    CheckParam(fxLayer, paramName, prop.IsDefaultEnabled);
                    if (prop.LocalOnly)
                        CheckParam(fxLayer, "IsLocal", false);

                    var layer = fxLayer.FindAnimatorControllerLayer(layerName);
                    if (layer == null)
                        layer = fxLayer.AddAnimatorControllerLayer(layerName);
                    var stateMachine = layer.stateMachine;
                    stateMachine.Clear();

                    if (variables.MenuMode == RISV3.RISMode.Simple)
                    {
                        ///
                        /// シンプルモード
                        /// 
                        var onState = stateMachine.AddState("PropON", new Vector3(300, 100, 0));
                        onState.writeDefaultValues = variables.WriteDefaults;
                        var offState = stateMachine.AddState("PropOFF", new Vector3(300, 200, 0));
                        offState.writeDefaultValues = variables.WriteDefaults;

                        var transition = stateMachine.MakeAnyStateTransition(onState);
                        transition.CreateSingleCondition(AnimatorConditionMode.If, paramName, 1f, prop.LocalOnly && !prop.IsDefaultEnabled, true);
                        transition = stateMachine.MakeAnyStateTransition(offState);
                        transition.CreateSingleCondition(AnimatorConditionMode.IfNot, paramName, 1f, prop.LocalOnly && prop.IsDefaultEnabled, true);

                        stateMachine.defaultState = prop.IsDefaultEnabled ? onState : offState;

                        var clip = new AnimationClip();
                        var curve = new AnimationCurve();
                        curve.AddKey(0f, 1);
                        curve.AddKey(1f / clip.frameRate, 1);
                        clip.SetCurve(prop.TargetObject.GetRelativePath(avatar.gameObject), typeof(GameObject), "m_IsActive", curve);
                        var clipName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "ON";
                        AssetDatabase.CreateAsset(clip, animationsFolder + clipName + ".anim");
                        onState.motion = clip;
                        EditorUtility.SetDirty(clip);

                        clip = new AnimationClip();
                        curve = new AnimationCurve();
                        curve.AddKey(0f, 0);
                        curve.AddKey(1f / clip.frameRate, 0);
                        clip.SetCurve(prop.TargetObject.GetRelativePath(avatar.gameObject), typeof(GameObject), "m_IsActive", curve);
                        clipName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "OFF";
                        AssetDatabase.CreateAsset(clip, animationsFolder + clipName + ".anim");
                        offState.motion = clip;
                        EditorUtility.SetDirty(clip);

                        if (group.ExclusiveMode == 2)
                        {
                            var driver = onState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                            foreach (var subPropIndex in Enumerable.Range(0, group.Props.Count))
                            {
                                if (propIndex != subPropIndex)
                                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                                    {
                                        name = $"RISV3-G{groupIndex}P{subPropIndex}",
                                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                                        value = 0
                                    });
                            }
                        }
                    }
                    else if(variables.MenuMode == RISV3.RISMode.Advanced)
                    {
                        ///
                        /// アドバンスモード
                        /// 
                        var onState = stateMachine.AddState("PropON", new Vector3(300, 100, 0));
                        onState.writeDefaultValues = variables.WriteDefaults;
                        var offState = stateMachine.AddState("PropOFF", new Vector3(300, 200, 0));
                        offState.writeDefaultValues = variables.WriteDefaults;

                        var transition = stateMachine.MakeAnyStateTransition(onState);
                        transition.CreateSingleCondition(AnimatorConditionMode.If, paramName, 1f, prop.LocalOnly && !prop.IsDefaultEnabled, true);
                        transition = stateMachine.MakeAnyStateTransition(offState);
                        transition.CreateSingleCondition(AnimatorConditionMode.IfNot, paramName, 1f, prop.LocalOnly && prop.IsDefaultEnabled, true);
                        if (prop.EnableAnimation != null || prop.DisableAnimation != null)
                        {
                            var additionalLayerName = $"RISV3-ANIM-G{groupIndex}P{propIndex}";
                            var additionalLayer = fxLayer.FindAnimatorControllerLayer(additionalLayerName);
                            if (additionalLayer == null)
                                additionalLayer = fxLayer.AddAnimatorControllerLayer(additionalLayerName);
                            var additionalStateMachine = additionalLayer.stateMachine;
                            additionalStateMachine.Clear();

                            var animOnState = additionalStateMachine.AddState("AnimON", new Vector3(300, 100, 0));
                            animOnState.writeDefaultValues = variables.WriteDefaults;
                            animOnState.motion = prop.EnableAnimation;
                            var animOffState = additionalStateMachine.AddState("AnimOFF", new Vector3(300, 200, 0));
                            animOffState.writeDefaultValues = variables.WriteDefaults;
                            animOffState.motion = prop.DisableAnimation;
                            additionalStateMachine.defaultState = prop.IsDefaultEnabled ? animOnState : animOffState;

                            transition = additionalStateMachine.MakeAnyStateTransition(animOnState);
                            transition.CreateSingleCondition(AnimatorConditionMode.If, paramName, 1f, prop.LocalOnly && !prop.IsDefaultEnabled, true);
                            transition = additionalStateMachine.MakeAnyStateTransition(animOffState);
                            transition.CreateSingleCondition(AnimatorConditionMode.IfNot, paramName, 1f, prop.LocalOnly && prop.IsDefaultEnabled, true);
                            EditorUtility.SetDirty(additionalLayer.stateMachine);

                        }

                        stateMachine.defaultState = prop.IsDefaultEnabled ? onState : offState;
                        if(prop.MaterialOverride == null)
                        {
                            var clipON = new AnimationClip();
                            var clipOFF = new AnimationClip();
                            foreach (var gameObject in prop.TargetObjects)
                            {
                                var curve = new AnimationCurve();
                                curve.AddKey(0f, 1);
                                curve.AddKey(1f / clipON.frameRate, 1);
                                clipON.SetCurve(gameObject.GetRelativePath(avatar.gameObject), typeof(GameObject), "m_IsActive", curve);

                                curve = new AnimationCurve();
                                curve.AddKey(0f, 0);
                                curve.AddKey(1f / clipOFF.frameRate, 0);
                                clipOFF.SetCurve(gameObject.GetRelativePath(avatar.gameObject), typeof(GameObject), "m_IsActive", curve);
                            }
                            var clipONName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "ON";
                            AssetDatabase.CreateAsset(clipON, animationsFolder + clipONName + ".anim");
                            onState.motion = clipON;
                            EditorUtility.SetDirty(clipON);

                            var clipOFFName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "OFF";
                            AssetDatabase.CreateAsset(clipOFF, animationsFolder + clipOFFName + ".anim");
                            offState.motion = clipOFF;
                            EditorUtility.SetDirty(clipOFF);
                        }
                        else
                        {
                            var clipON = new AnimationClip();
                            var clipOFF = new AnimationClip();
                            foreach (var gameObject in prop.TargetObjects)
                            {
                                var renderer = gameObject.GetComponent<Renderer>();
                                if (renderer != null)
                                {
                                    var baseMaterialPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                                    var baseMaterial = AssetDatabase.LoadAssetAtPath(baseMaterialPath, typeof(Material)) as Material;
                                    if (baseMaterial != null)
                                    {
                                        var baseMaterialProperties = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { baseMaterial });
                                        var addMaterialProperties = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { prop.MaterialOverride });
                                        var path = gameObject.GetRelativePath(avatar.gameObject);
                                        var renderType = typeof(Renderer);
                                        if (gameObject.GetComponent<MeshRenderer>() != null)
                                            renderType = typeof(MeshRenderer);
                                        if (gameObject.GetComponent<SkinnedMeshRenderer>() != null)
                                            renderType = typeof(SkinnedMeshRenderer);
                                        EditorCurveBinding curveBinding = EditorCurveBinding.PPtrCurve(path, renderType, "m_Materials.Array.data[0]");
                                        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[2];
                                        keyFrames[0] = new ObjectReferenceKeyframe() { time = 0f, value = prop.MaterialOverride };
                                        keyFrames[1] = new ObjectReferenceKeyframe() { time = 1f / clipON.frameRate, value = prop.MaterialOverride };
                                        AnimationUtility.SetObjectReferenceCurve(clipON, curveBinding, keyFrames);
                                        foreach(var property in addMaterialProperties.Where(addMat => baseMaterialProperties.Any(baseMat => baseMat.name == addMat.name)))
                                        {
                                            var baseMatProperty = baseMaterialProperties.First(n => n.name == property.name);
                                            if (property.type == MaterialProperty.PropType.Color)
                                            {
                                                if (baseMatProperty.colorValue.r != property.colorValue.r || baseMatProperty.colorValue.g != property.colorValue.g ||
                                                    baseMatProperty.colorValue.g != property.colorValue.b || baseMatProperty.colorValue.g != property.colorValue.a)
                                                {
                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.r");
                                                    var curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.r);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.r);
                                                    AnimationUtility.SetEditorCurve(clipON, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.g");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.g);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.g);
                                                    AnimationUtility.SetEditorCurve(clipON, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.b");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.b);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.b);
                                                    AnimationUtility.SetEditorCurve(clipON, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.a");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.a);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.a);
                                                    AnimationUtility.SetEditorCurve(clipON, curveBinding, curve);
                                                }
                                            }
                                            if (property.type == MaterialProperty.PropType.Float)
                                            {
                                                if (baseMatProperty.floatValue != property.floatValue)
                                                {
                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}");
                                                    var curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.floatValue);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.floatValue);
                                                    AnimationUtility.SetEditorCurve(clipON, curveBinding, curve);
                                                }
                                            }
                                        }

                                        curveBinding = EditorCurveBinding.PPtrCurve(path, renderType, "m_Materials.Array.data[0]");
                                        keyFrames = new ObjectReferenceKeyframe[2];
                                        keyFrames[0] = new ObjectReferenceKeyframe() { time = 0f, value = baseMaterial };
                                        keyFrames[1] = new ObjectReferenceKeyframe() { time = 1f / clipON.frameRate, value = baseMaterial };
                                        AnimationUtility.SetObjectReferenceCurve(clipOFF, curveBinding, keyFrames);

                                        foreach (var property in baseMaterialProperties.Where(baseMat => addMaterialProperties.Any(addMat => baseMat.name == addMat.name)))
                                        {
                                            var addMatProperty = addMaterialProperties.First(n => n.name == property.name);
                                            if (property.type == MaterialProperty.PropType.Color)
                                            {
                                                if (addMatProperty.colorValue.r != property.colorValue.r || addMatProperty.colorValue.g != property.colorValue.g ||
                                                    addMatProperty.colorValue.g != property.colorValue.b || addMatProperty.colorValue.g != property.colorValue.a)
                                                {
                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.r");
                                                    var curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.r);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.r);
                                                    AnimationUtility.SetEditorCurve(clipOFF, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.g");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.g);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.g);
                                                    AnimationUtility.SetEditorCurve(clipOFF, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.b");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.b);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.b);
                                                    AnimationUtility.SetEditorCurve(clipOFF, curveBinding, curve);

                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}.a");
                                                    curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.colorValue.a);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.colorValue.a);
                                                    AnimationUtility.SetEditorCurve(clipOFF, curveBinding, curve);
                                                }
                                            }
                                            if (property.type == MaterialProperty.PropType.Float)
                                            {
                                                if (addMatProperty.floatValue != property.floatValue)
                                                {
                                                    curveBinding = EditorCurveBinding.FloatCurve(path, renderType, $"material.{property.name}");
                                                    var curve = new AnimationCurve();
                                                    curve.AddKey(0f, property.floatValue);
                                                    curve.AddKey(1f / clipOFF.frameRate, property.floatValue);
                                                    AnimationUtility.SetEditorCurve(clipOFF, curveBinding, curve);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            var clipONName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "ON";
                            AssetDatabase.CreateAsset(clipON, animationsFolder + clipONName + ".anim");
                            onState.motion = clipON;
                            EditorUtility.SetDirty(clipON);

                            var clipOFFName = "G" + groupIndex.ToString() + "P" + propIndex.ToString() + "OFF";
                            AssetDatabase.CreateAsset(clipOFF, animationsFolder + clipOFFName + ".anim");
                            offState.motion = clipOFF;
                            EditorUtility.SetDirty(clipOFF);
                        }

                        if (prop.PropGroupType != RISV3.PropGroup.None)
                        {
                            var driver = onState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                            foreach (var subGroupIndex in Enumerable.Range(0, variables.Groups.Count))
                            {
                                var subGroup = variables.Groups[subGroupIndex];
                                foreach (var subPropIndex in Enumerable.Range(0, subGroup.Props.Count))
                                {
                                    var subProp = subGroup.Props[subPropIndex];
                                    if (propIndex != subPropIndex && subProp.PropGroupType == prop.PropGroupType)
                                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                                        {
                                            name = $"RISV3-G{groupIndex}P{subPropIndex}",
                                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                                            value = 0
                                        });
                                }
                            }
                        }
                        if (prop.UseResetTimer)
                        {
                            var timerLayerName = $"RISV3-TIMER-G{groupIndex}P{propIndex}";
                            var timerLayer = fxLayer.FindAnimatorControllerLayer(timerLayerName);
                            if (timerLayer == null)
                                timerLayer = fxLayer.AddAnimatorControllerLayer(timerLayerName);
                            var timerStateMachine = timerLayer.stateMachine;
                            timerStateMachine.Clear();

                            var waitState = timerStateMachine.AddState("WaitTimer", new Vector3(300, 100, 0));
                            waitState.writeDefaultValues = variables.WriteDefaults;
                            var countdownState = timerStateMachine.AddState("Countdown", new Vector3(300, 200, 0));
                            countdownState.writeDefaultValues = variables.WriteDefaults;
                            var stopState = timerStateMachine.AddState("StopTimer", new Vector3(600, 200, 0));
                            stopState.writeDefaultValues = variables.WriteDefaults;

                            transition = countdownState.MakeTransition(stopState);
                            transition.exitTime = prop.ResetSecond;
                            transition.hasExitTime = true;
                            transition.hasFixedDuration = true;

                            transition = timerStateMachine.MakeAnyStateTransition(waitState);
                            transition.CreateSingleCondition(prop.IsDefaultEnabled ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, paramName, 1f, false, false);
                            transition = timerStateMachine.MakeAnyStateTransition(countdownState);
                            transition.CreateSingleCondition(prop.IsDefaultEnabled ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, paramName, 1f, false, false);
                            
                            var driver = stopState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                            driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                            {
                                name = $"RISV3-G{groupIndex}P{propIndex}",
                                type = VRC_AvatarParameterDriver.ChangeType.Set,
                                value = prop.IsDefaultEnabled ? 1 : 0
                            });
                            EditorUtility.SetDirty(timerLayer.stateMachine);
                        }
                    }
                    layer.stateMachine = stateMachine;
                    EditorUtility.SetDirty(layer.stateMachine);
                }
                if (group.ExclusiveMode == 1 && variables.MenuMode == RISV3.RISMode.Simple)
                {
                    var layerName = $"RISV3-RESET-G{groupIndex}";

                    var paramName = $"RISV3-G{groupIndex}RESET";
                    CheckParam(fxLayer, paramName, false);

                    var layer = fxLayer.FindAnimatorControllerLayer(layerName);
                    if (layer == null)
                        layer = fxLayer.AddAnimatorControllerLayer(layerName);
                    var stateMachine = layer.stateMachine;
                    stateMachine.Clear();

                    if (variables.MenuMode == RISV3.RISMode.Simple)
                    {
                        var onState = stateMachine.AddState("Reset", new Vector3(300, 100, 0));
                        onState.writeDefaultValues = variables.WriteDefaults;
                        var offState = stateMachine.AddState("Wait", new Vector3(300, 200, 0));
                        offState.writeDefaultValues = variables.WriteDefaults;

                        var transition = stateMachine.MakeAnyStateTransition(onState);
                        transition.CreateSingleCondition(AnimatorConditionMode.If, paramName, 1f, false, false);
                        transition = stateMachine.MakeAnyStateTransition(offState);
                        transition.CreateSingleCondition(AnimatorConditionMode.IfNot, paramName, 1f, false, false);

                        stateMachine.defaultState = offState;

                        var driver = onState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                        driver.parameters =
                            Enumerable.Range(0, group.Props.Count).Select(num => new VRC_AvatarParameterDriver.Parameter()
                            {
                                name = $"RISV3-G{groupIndex}P{num}",
                                type = VRC_AvatarParameterDriver.ChangeType.Set,
                                value = group.Props[num].IsDefaultEnabled ? 1 : 0
                            }).ToList();
                        driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter()
                        {
                            name = $"RISV3-G{groupIndex}RESET",
                            type = VRC_AvatarParameterDriver.ChangeType.Set,
                            value = 0
                        });
                    }
                }
            }
            avatar.baseAnimationLayers[4].animatorController = fxLayer;
            EditorUtility.SetDirty(avatar.baseAnimationLayers[3].animatorController);
            EditorUtility.SetDirty(avatar);
        }

        private static void CheckParam(AnimatorController controller, string paramName, bool defaultEnabled)
        {
            var param = controller.parameters.FirstOrDefault(n => n.name == paramName);
            if (param == null)
            {
                controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
                param = controller.parameters.FirstOrDefault(n => n.name == paramName);
            }
            param.type = AnimatorControllerParameterType.Bool;
            param.defaultBool = defaultEnabled;
        }

        private static void BuildExpressionParameters(RISVariables variables, string autoGeneratedFolder)
        {
            var avatar = variables.AvatarRoot;

            var expParams = avatar.GetExpressionParameters(autoGeneratedFolder);
            if (variables.OptimizeParams)
                expParams.OptimizeParameter();
            foreach (var name in expParams.parameters.Where(n => n.name.StartsWith("RISV3")).Select(n => n.name))
                expParams.TryRemoveParameter(name);
            foreach (var groupIndex in Enumerable.Range(0, variables.Groups.Count))
            {
                var group = variables.Groups[groupIndex];
                if (variables.MenuMode == RISV3.RISMode.Simple && group.ExclusiveMode == 1)
                    TryAddParam(variables, $"RISV3-G{groupIndex}RESET", 0f, false);
                foreach (var propIndex in Enumerable.Range(0, group.Props.Count))
                {
                    var prop = group.Props[propIndex];
                    TryAddParam(variables, $"RISV3-G{groupIndex}P{propIndex}", prop.IsDefaultEnabled ? 1f : 0f, prop.SaveParameter);
                }
            }
            avatar.expressionParameters = expParams;
            EditorUtility.SetDirty(avatar);
            EditorUtility.SetDirty(avatar.expressionParameters);
        }
        private static void BuildExpressionsMenu(RISVariables variables, string autoGeneratedFolder)
        {
            var avatar = variables.AvatarRoot;

            VRCExpressionsMenu menu = null;
            var rootMenu = avatar.expressionsMenu;
            if (rootMenu == null)
                rootMenu = UnityUtils.TryGetAsset(autoGeneratedFolder + "AutoGeneratedMenu.asset", typeof(VRCExpressionsMenu)) as VRCExpressionsMenu;
            var risControl = rootMenu.controls.FirstOrDefault(n => n.name == "Radiai Inventory");
            if (risControl == null && rootMenu.controls.Count > 0)
                rootMenu.controls.Add(risControl = new VRCExpressionsMenu.Control() { name = "Radiai Inventory" });

            if (risControl == null && rootMenu.controls.Count == 0)
                menu = rootMenu;
            else
            {
                if (risControl == null && rootMenu.controls.Count > 0)
                    rootMenu.controls.Add(risControl = new VRCExpressionsMenu.Control() { name = "Radiai Inventory" });
                risControl.icon = menuIcon;
                risControl.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                risControl.subMenu = menu = UnityUtils.TryGetAsset(autoGeneratedFolder + $"RadInvMainMenu.asset", typeof(VRCExpressionsMenu)) as VRCExpressionsMenu;
            }
            var subMenuFolder = autoGeneratedFolder + "SubMenus/";
            UnityUtils.ReCreateFolder(subMenuFolder);
            menu.controls.Clear();
            foreach(var groupIndex in Enumerable.Range(0, variables.Groups.Count))
            {
                var group = variables.Groups[groupIndex];
                var groupName = group.GroupName;
                if (string.IsNullOrWhiteSpace(groupName))
                    groupName = "Group" + groupIndex.ToString();

                VRCExpressionsMenu.Control control = new VRCExpressionsMenu.Control();
                control.name = groupName;
                control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                control.icon = group.GroupIcon;
                if (control.icon == null)
                    control.icon = groupIcon;
                if (variables.MenuMode == RISV3.RISMode.Advanced && group.BaseMenu != null)
                    AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(group.BaseMenu), subMenuFolder + $"Group{groupIndex}Menu.asset");
                VRCExpressionsMenu subMenu = control.subMenu = UnityUtils.TryGetAsset(subMenuFolder + $"Group{groupIndex}Menu.asset", typeof(VRCExpressionsMenu)) as VRCExpressionsMenu;
                if(group.ExclusiveMode == 1 && variables.MenuMode == RISV3.RISMode.Simple)
                {
                    var propControl = new VRCExpressionsMenu.Control();
                    propControl.name = "Reset";
                    propControl.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    propControl.value = 1f;
                    propControl.icon = reloadIcon;
                    propControl.parameter = new VRCExpressionsMenu.Control.Parameter() { name = $"RISV3-G{groupIndex}RESET" };
                    subMenu.controls.Add(propControl);
                }
                foreach (var propIndex in Enumerable.Range(0,group.Props.Count))
                {
                    var prop = group.Props[propIndex];
                    var propName = prop.GetPropName();
                    if (string.IsNullOrWhiteSpace(propName))
                        propName = "Prop" + propIndex.ToString();
                    var propControl = new VRCExpressionsMenu.Control();
                    propControl.name = propName;
                    propControl.type = VRCExpressionsMenu.Control.ControlType.Toggle;
                    propControl.value = 1f;
                    propControl.parameter = new VRCExpressionsMenu.Control.Parameter() { name = $"RISV3-G{groupIndex}P{propIndex}"};
                    propControl.icon = prop.PropIcon;
                    if (propControl.icon == null)
                        propControl.icon = boxIcon;
                    subMenu.controls.Add(propControl);
                }
                control.subMenu = subMenu;
                menu.controls.Add(control);
                EditorUtility.SetDirty(control.subMenu);
            }
            avatar.expressionsMenu = rootMenu;
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(avatar.expressionsMenu);
            EditorUtility.SetDirty(avatar);
        }

        private static void TryAddParam(RISVariables variables, string name, float defaultValue, bool saved)
        {
            var expParams = variables.AvatarRoot.expressionParameters;
            if (variables.OptimizeParams)
            {
                var existParam = expParams.FindParameter(name);
                if(existParam != null)
                {
                    existParam.saved = saved;
                    existParam.valueType = VRCExpressionParameters.ValueType.Bool;
                    existParam.defaultValue = defaultValue;
                }
                else
                {
                    expParams.AddParameter(name, VRCExpressionParameters.ValueType.Bool, saved, defaultValue);
                }

            }
            else
                expParams.AddParameter(name, VRCExpressionParameters.ValueType.Bool, saved, defaultValue);

        }
    }
}
