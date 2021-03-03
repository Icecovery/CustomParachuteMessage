Shader "MessageChute/MessageChute" 
{
     Properties {
        _tex_normal ("tex_normal", 2D) = "bump" {}
        _tex_ao ("tex_ao", 2D) = "white" {}
        _transmission ("transmission", Float ) = 0.5
        _ambientBase ("ambientBase", Float ) = 0.2
        _color_A ("color_A", Color) = (1,1,1,1)
        _color_B ("color_B", Color) = (1,0.4189905,0,1)
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }
        LOD 200
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            Cull Off
            
            
            CGPROGRAM

            float data[320];

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdbase_fullshadows
            #pragma multi_compile_fog
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _tex_normal; uniform float4 _tex_normal_ST;
            uniform sampler2D _tex_ao; uniform float4 _tex_ao_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float, _transmission)
                UNITY_DEFINE_INSTANCED_PROP( float, _ambientBase)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_A)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_B)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
                i.normalDir = normalize(i.normalDir);
                i.normalDir *= faceSign;
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _tex_normal_var = UnpackNormal(tex2D(_tex_normal,TRANSFORM_TEX(i.uv0, _tex_normal)));
                float3 normalLocal = _tex_normal_var.rgb;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = dot( normalDirection, lightDirection );
                float3 forwardLight = max(0.0, NdotL );
                float _transmission_var = UNITY_ACCESS_INSTANCED_PROP( Props, _transmission );
                float3 backLight = max(0.0, -NdotL ) * float3(_transmission_var,_transmission_var,_transmission_var);
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = (forwardLight+backLight) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += UNITY_LIGHTMODEL_AMBIENT.rgb; // Ambient Light
                float _ambientBase_var = UNITY_ACCESS_INSTANCED_PROP( Props, _ambientBase );
                indirectDiffuse += _ambientBase_var * lightColor * _LightColor0.w; // Diffuse Ambient Light //change me
                float4 _color_A_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_A );
                float4 _color_B_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_B );
                float4 _tex_ao_var = tex2D(_tex_ao,TRANSFORM_TEX(i.uv0, _tex_ao));

                float nS = 80;
                float nR = 4;
                float un = i.uv0.r * nS;
                float vn = i.uv0.g * nR;

                int segmentNumber = floor(un);      
                int ringNumber = nR - 1 - floor(vn); 

                float value = data[segmentNumber + ringNumber * nS];

                int left =  abs(value - data[fmod((segmentNumber - 1), nS) + (ringNumber)     * nS]);
                int right = abs(value - data[fmod((segmentNumber + 1), nS) + (ringNumber)     * nS]);
                int up =    abs(value - data[fmod((segmentNumber)    , nS) + (ringNumber + 1) * nS]);
                int down =  abs(value - data[fmod((segmentNumber)    , nS) + (ringNumber - 1) * nS]);
                
                float vM = 4;
                float dist = 1.0;
                dist = min(dist, ((1 - left) * dist) + (left *  frac(un)          ));
                dist = min(dist, ((1 - right)* dist) + (right * (ceil(un) - un)   ));
                dist = min(dist, ((1 - up)   * dist) + (up *    frac(vn)          ) * vM);
                dist = min(dist, ((1 - down) * dist) + (down *  (ceil(vn) - vn)   ) * vM);


                float smoothness = 0.1;
                float pixelDist = smoothstep(-smoothness, smoothness, dist);

                value = value * pixelDist;
                

                float3 diffuseColor = saturate((lerp(_color_A_var.rgb,_color_B_var.rgb,value)*_tex_ao_var.rgb));
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor,1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            Cull Off
            
            
            CGPROGRAM

            float data[320];

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma target 3.0
            uniform float4 _LightColor0;
            uniform sampler2D _tex_normal; uniform float4 _tex_normal_ST;
            uniform sampler2D _tex_ao; uniform float4 _tex_ao_ST;
            UNITY_INSTANCING_BUFFER_START( Props )
                UNITY_DEFINE_INSTANCED_PROP( float, _transmission)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_A)
                UNITY_DEFINE_INSTANCED_PROP( float4, _color_B)
            UNITY_INSTANCING_BUFFER_END( Props )
            struct VertexInput {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                LIGHTING_COORDS(5,6)
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                UNITY_SETUP_INSTANCE_ID( v );
                UNITY_TRANSFER_INSTANCE_ID( v, o );
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                UNITY_SETUP_INSTANCE_ID( i );
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
                i.normalDir = normalize(i.normalDir);
                i.normalDir *= faceSign;
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 _tex_normal_var = UnpackNormal(tex2D(_tex_normal,TRANSFORM_TEX(i.uv0, _tex_normal)));
                float3 normalLocal = _tex_normal_var.rgb;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
/////// Diffuse:
                float NdotL = dot( normalDirection, lightDirection );
                float3 forwardLight = max(0.0, NdotL );
                float _transmission_var = UNITY_ACCESS_INSTANCED_PROP( Props, _transmission );
                float3 backLight = max(0.0, -NdotL ) * float3(_transmission_var,_transmission_var,_transmission_var);
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                float3 directDiffuse = (forwardLight+backLight) * attenColor;
                float4 _color_A_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_A );
                float4 _color_B_var = UNITY_ACCESS_INSTANCED_PROP( Props, _color_B );
                float4 _tex_ao_var = tex2D(_tex_ao,TRANSFORM_TEX(i.uv0, _tex_ao));

                float nS = 80;
                float nR = 4;
                float un = i.uv0.r * nS;
                float vn = i.uv0.g * nR;

                int segmentNumber = floor(un);      
                int ringNumber = nR - 1 - floor(vn); 

                float value = data[segmentNumber + ringNumber * nS];

                int left =  abs(value - data[fmod((segmentNumber - 1), nS) + (ringNumber)     * nS]);
                int right = abs(value - data[fmod((segmentNumber + 1), nS) + (ringNumber)     * nS]);
                int up =    abs(value - data[fmod((segmentNumber)    , nS) + (ringNumber + 1) * nS]);
                int down =  abs(value - data[fmod((segmentNumber)    , nS) + (ringNumber - 1) * nS]);
                
                float vM = 4;
                float dist = 1.0;
                dist = min(dist, ((1 - left) * dist) + (left *  frac(un)          ));
                dist = min(dist, ((1 - right)* dist) + (right * (ceil(un) - un)   ));
                dist = min(dist, ((1 - up)   * dist) + (up *    frac(vn)          ) * vM);
                dist = min(dist, ((1 - down) * dist) + (down *  (ceil(vn) - vn)   ) * vM);


                float smoothness = 0.1;
                float pixelDist = smoothstep(-smoothness, smoothness, dist);

                value = value * pixelDist;

                float3 diffuseColor = saturate((lerp(_color_A_var.rgb,_color_B_var.rgb,value)*_tex_ao_var.rgb));
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse;
                fixed4 finalRGBA = fixed4(finalColor * 1,0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }
            Offset 1, 1
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_fog
            #pragma target 3.0
            struct VertexInput {
                float4 vertex : POSITION;
            };
            struct VertexOutput {
                V2F_SHADOW_CASTER;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.pos = UnityObjectToClipPos( v.vertex );
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }
            float4 frag(VertexOutput i, float facing : VFACE) : COLOR {
                float isFrontFace = ( facing >= 0 ? 1 : 0 );
                float faceSign = ( facing >= 0 ? 1 : -1 );
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
