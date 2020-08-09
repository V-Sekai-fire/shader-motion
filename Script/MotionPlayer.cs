using System.Collections.Generic;
using System.Linq;
using Array = System.Array;
using UnityEngine;
using Unity.Collections;
using AsyncGPUReadbackRequest = UnityEngine.Rendering.AsyncGPUReadbackRequest;

namespace ShaderMotion {
public class MotionPlayer {
	public Quaternion rootQ;
	public Vector3 rootT;
	public Vector3[] muscles;
	public SkinnedMeshRenderer shapeRenderer = null;

	HumanUtil.Armature armature;
	FrameLayout layout;
	HumanPoseHandler poseHandler;
	HumanPose pose;
	public MotionPlayer(HumanUtil.Armature armature, FrameLayout layout) {
		this.armature = armature;
		this.layout = layout;
		poseHandler = new HumanPoseHandler(armature.root.GetComponent<Animator>().avatar, armature.root);
		poseHandler.GetHumanPose(ref pose);

		muscles = new Vector3[armature.bones.Length];
	}

	static int width = 40, height = 45;
	NativeArray<Color> colors = new NativeArray<Color>();
	float SampleSlot(int idx) {
		int x = idx / height;
		int y = idx % height;
		return colors[x + (height-1-y) * width].r;
	}	
	public void Update(AsyncGPUReadbackRequest req) {
		colors = req.GetData<Color>();

		rootT = new Vector3(0,1,0);
		var rootY = Vector3.up;
		var rootZ = Vector3.forward;

		Array.Clear(muscles, 0, muscles.Length);
		for(int i=0; i<armature.bones.Length; i++) {
			var slot = layout.baseIndices[i];
			foreach(var j in layout.channels[i]) {
				var v = SampleSlot(slot);
				if(j<3)
					muscles[i][j] = v * 180;
				else if(j<6) {
					// TODO: high precision not implemented
				}
				else if(j<9)
					rootT[j-6] = v * 2;
				else if(j<12)
					rootY[j-9] = v;
				else if(j<15)
					rootZ[j-12] = v;

				slot++;
			}
		}
		rootQ = Quaternion.LookRotation(rootZ, rootY);
	}
	public void ApplyTransform() {
		for(int i=0; i<armature.bones.Length; i++)
			if(armature.bones[i]) {
				var axes = armature.axes[i];
				if(armature.humanBones[i] == HumanBodyBones.Hips)
					armature.bones[i].SetPositionAndRotation(
						armature.root.TransformPoint(rootT * armature.scale),
						armature.root.rotation * rootQ * Quaternion.Inverse(axes.postQ));
				else
					armature.bones[i].localRotation = axes.preQ * muscleToRotation(axes.sign * muscles[i])
												* Quaternion.Inverse(axes.postQ);
			}
	}
	public void ApplyHumanPose() {
		pose.bodyPosition = rootT;
		pose.bodyRotation = rootQ;
		Array.Clear(pose.muscles, 0, pose.muscles.Length);
		for(int i=0; i<armature.bones.Length; i++)
			for(int j=0; j<3; j++) {
				var muscle = boneMuscles[(int)armature.humanBones[i], j];
				if(muscle >= 0)
					pose.muscles[muscle] += muscles[i][j];
			}
		for(int i=0; i<HumanTrait.MuscleCount; i++)
			pose.muscles[i] /= pose.muscles[i] >= 0 ? muscleLimits[i,1] : -muscleLimits[i,0];
		poseHandler.SetHumanPose(ref pose);
	}

	float[] shapeWeights = null;
	public void ApplyBlendShape() {
		var mesh = shapeRenderer?.sharedMesh;
		if(mesh != null && layout.shapeIndices != null && layout.shapeIndices.Count != 0) {
			Array.Resize(ref shapeWeights, mesh.blendShapeCount);
			for(int shape=0; shape<shapeWeights.Length; shape++)
				shapeWeights[shape] = float.NaN;
			foreach(var si in layout.shapeIndices) {
				var shape = mesh.GetBlendShapeIndex(si.shape);
				if(shape >= 0) {
					var v = SampleSlot(si.index);
					if(float.IsNaN(shapeWeights[shape]))
						shapeWeights[shape] = 0;
					shapeWeights[shape] += v * si.weight;
				}
			}
			for(int shape=0; shape<shapeWeights.Length; shape++)
				if(!float.IsNaN(shapeWeights[shape])) {
					var frame = mesh.GetBlendShapeFrameCount(shape)-1;
					var weight = mesh.GetBlendShapeFrameWeight(shape, frame);
					shapeRenderer.SetBlendShapeWeight(shape, shapeWeights[shape] * weight);
				}
		}
	}
	Quaternion muscleToRotation(Vector3 muscle) {
		var muscleYZ = new Vector3(0, muscle.y, muscle.z);
		return Quaternion.AngleAxis(muscleYZ.magnitude, muscleYZ.normalized)
				* Quaternion.AngleAxis(muscle.x, new Vector3(1,0,0));
	}

	static int[,] boneMuscles;
	static float[,] muscleLimits;
	static MotionPlayer() {
		boneMuscles = new int[HumanTrait.BoneCount, 3];
		for(int i=0; i<HumanTrait.BoneCount; i++) 
			for(int j=0; j<3; j++) {
				var hb = i;
				var muscle = HumanTrait.MuscleFromBone(hb, j);
				if(muscle < 0) {
					switch(hb) {
					case (int)HumanBodyBones.LeftShoulder:
						hb = (int)HumanBodyBones.LeftUpperArm; break;
					case (int)HumanBodyBones.RightShoulder:
						hb = (int)HumanBodyBones.RightUpperArm; break;
					case (int)HumanBodyBones.Jaw:
						break;
					default:
						hb = HumanTrait.GetParentBone(hb);break;
					}
					muscle = HumanTrait.MuscleFromBone(hb, j);
				}
				boneMuscles[i, j] = muscle;
			}
		muscleLimits = new float[HumanTrait.MuscleCount, 2];
		for(int i=0; i<HumanTrait.MuscleCount; i++) {
			muscleLimits[i, 0] = HumanTrait.GetMuscleDefaultMin(i);
			muscleLimits[i, 1] = HumanTrait.GetMuscleDefaultMax(i);
		}
	}
}
}