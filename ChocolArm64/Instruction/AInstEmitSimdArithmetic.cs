// https://github.com/intel/ARM_NEON_2_x86_SSE/blob/master/NEON_2_SSE.h

using ChocolArm64.Decoder;
using ChocolArm64.State;
using ChocolArm64.Translation;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using static ChocolArm64.Instruction.AInstEmitSimdHelper;

namespace ChocolArm64.Instruction
{
    static partial class AInstEmit
    {
        public static void Abs_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpSx(Context, () => EmitAbs(Context));
        }

        public static void Abs_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpSx(Context, () => EmitAbs(Context));
        }

        public static void Add_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpZx(Context, () => Context.Emit(OpCodes.Add));
        }

        public static void Add_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse2)
            {
                EmitSse2Op(Context, nameof(Sse2.Add));
            }
            else
            {
                EmitVectorBinaryOpZx(Context, () => Context.Emit(OpCodes.Add));
            }
        }

        public static void Addhn_V(AILEmitterCtx Context)
        {
            EmitHighNarrow(Context, () => Context.Emit(OpCodes.Add), Round: false);
        }

        public static void Addp_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitVectorExtractZx(Context, Op.Rn, 0, Op.Size);
            EmitVectorExtractZx(Context, Op.Rn, 1, Op.Size);

            Context.Emit(OpCodes.Add);

            EmitScalarSet(Context, Op.Rd, Op.Size);
        }

        public static void Addp_V(AILEmitterCtx Context)
        {
            EmitVectorPairwiseOpZx(Context, () => Context.Emit(OpCodes.Add));
        }

        public static void Addv_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Bytes = Op.GetBitsCount() >> 3;
            int Elems = Bytes >> Op.Size;

            EmitVectorExtractZx(Context, Op.Rn, 0, Op.Size);

            for (int Index = 1; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size);

                Context.Emit(OpCodes.Add);
            }

            EmitScalarSet(Context, Op.Rd, Op.Size);
        }

        public static void Cls_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Bytes = Op.GetBitsCount() >> 3;
            int Elems = Bytes >> Op.Size;

            int ESize = 8 << Op.Size;

            for (int Index = 0; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size);

                Context.EmitLdc_I4(ESize);

                ASoftFallback.EmitCall(Context, nameof(ASoftFallback.CountLeadingSigns));

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Clz_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Bytes = Op.GetBitsCount() >> 3;
            int Elems = Bytes >> Op.Size;

            int ESize = 8 << Op.Size;

            for (int Index = 0; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size);

                if (Lzcnt.IsSupported && ESize == 32)
                {
                    Context.Emit(OpCodes.Conv_U4);

                    Context.EmitCall(typeof(Lzcnt).GetMethod(nameof(Lzcnt.LeadingZeroCount), new Type[] { typeof(uint) }));

                    Context.Emit(OpCodes.Conv_U8);
                }
                else
                {
                    Context.EmitLdc_I4(ESize);

                    ASoftFallback.EmitCall(Context, nameof(ASoftFallback.CountLeadingZeros));
                }

                EmitVectorInsert(Context, Op.Rd, Index, Op.Size);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Cnt_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Elems = Op.RegisterSize == ARegisterSize.SIMD128 ? 16 : 8;

            for (int Index = 0; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, 0);

                if (Popcnt.IsSupported)
                {
                    Context.EmitCall(typeof(Popcnt).GetMethod(nameof(Popcnt.PopCount), new Type[] { typeof(ulong) }));
                }
                else
                {
                    ASoftFallback.EmitCall(Context, nameof(ASoftFallback.CountSetBits8));
                }

                EmitVectorInsert(Context, Op.Rd, Index, 0);
            }

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        public static void Fabd_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpF(Context, () =>
            {
                Context.Emit(OpCodes.Sub);

                EmitUnaryMathCall(Context, nameof(Math.Abs));
            });
        }

        public static void Fabs_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Abs));
            });
        }

        public static void Fabs_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Abs));
            });
        }

        public static void Fadd_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.AddScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPAdd));
                });
            }
        }

        public static void Fadd_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Add));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPAdd));
                });
            }
        }

        public static void Faddp_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int SizeF = Op.Size & 1;

            EmitVectorExtractF(Context, Op.Rn, 0, SizeF);
            EmitVectorExtractF(Context, Op.Rn, 1, SizeF);

            Context.Emit(OpCodes.Add);

            EmitScalarSetF(Context, Op.Rd, SizeF);
        }

        public static void Faddp_V(AILEmitterCtx Context)
        {
            EmitVectorPairwiseOpF(Context, () => Context.Emit(OpCodes.Add));
        }

        public static void Fdiv_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.DivideScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPDiv));
                });
            }
        }

        public static void Fdiv_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Divide));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPDiv));
                });
            }
        }

        public static void Fmadd_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                if (Op.Size == 0)
                {
                    Type[] TypesMulAdd = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdvec(Op.Ra);
                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.MultiplyScalar), TypesMulAdd));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.AddScalar),      TypesMulAdd));

                    Context.EmitStvec(Op.Rd);

                    EmitVectorZero32_128(Context, Op.Rd);
                }
                else /* if (Op.Size == 1) */
                {
                    Type[] TypesMulAdd = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    EmitLdvecWithCastToDouble(Context, Op.Ra);
                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.MultiplyScalar), TypesMulAdd));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.AddScalar),      TypesMulAdd));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);

                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitScalarTernaryRaOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulAdd));
                });
            }
        }

        public static void Fmax_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.MaxScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMax));
                });
            }
        }

        public static void Fmax_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Max));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMax));
                });
            }
        }

        public static void Fmaxnm_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMaxNum));
            });
        }

        public static void Fmaxnm_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMaxNum));
            });
        }

        public static void Fmaxp_V(AILEmitterCtx Context)
        {
            EmitVectorPairwiseOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMax));
            });
        }

        public static void Fmin_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.MinScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMin));
                });
            }
        }

        public static void Fmin_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Min));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMin));
                });
            }
        }

        public static void Fminnm_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMinNum));
            });
        }

        public static void Fminnm_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMinNum));
            });
        }

        public static void Fminp_V(AILEmitterCtx Context)
        {
            EmitVectorPairwiseOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMin));
            });
        }

        public static void Fmla_Se(AILEmitterCtx Context)
        {
            EmitScalarTernaryOpByElemF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Add);
            });
        }

        public static void Fmla_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Add);
            });
        }

        public static void Fmla_Ve(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpByElemF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Add);
            });
        }

        public static void Fmls_Se(AILEmitterCtx Context)
        {
            EmitScalarTernaryOpByElemF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Sub);
            });
        }

        public static void Fmls_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Sub);
            });
        }

        public static void Fmls_Ve(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpByElemF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Sub);
            });
        }

        public static void Fmsub_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                if (Op.Size == 0)
                {
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdvec(Op.Ra);
                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SubtractScalar), TypesMulSub));

                    Context.EmitStvec(Op.Rd);

                    EmitVectorZero32_128(Context, Op.Rd);
                }
                else /* if (Op.Size == 1) */
                {
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    EmitLdvecWithCastToDouble(Context, Op.Ra);
                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SubtractScalar), TypesMulSub));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);

                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitScalarTernaryRaOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulSub));
                });
            }
        }

        public static void Fmul_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.MultiplyScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMul));
                });
            }
        }

        public static void Fmul_Se(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpByElemF(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Fmul_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Multiply));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMul));
                });
            }
        }

        public static void Fmul_Ve(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpByElemF(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Fmulx_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulX));
            });
        }

        public static void Fmulx_Se(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpByElemF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulX));
            });
        }

        public static void Fmulx_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulX));
            });
        }

        public static void Fmulx_Ve(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpByElemF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPMulX));
            });
        }

        public static void Fneg_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Fneg_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Fnmadd_S(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int SizeF = Op.Size & 1;

            EmitVectorExtractF(Context, Op.Rn, 0, SizeF);

            Context.Emit(OpCodes.Neg);

            EmitVectorExtractF(Context, Op.Rm, 0, SizeF);

            Context.Emit(OpCodes.Mul);

            EmitVectorExtractF(Context, Op.Ra, 0, SizeF);

            Context.Emit(OpCodes.Sub);

            EmitScalarSetF(Context, Op.Rd, SizeF);
        }

        public static void Fnmsub_S(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int SizeF = Op.Size & 1;

            EmitVectorExtractF(Context, Op.Rn, 0, SizeF);
            EmitVectorExtractF(Context, Op.Rm, 0, SizeF);

            Context.Emit(OpCodes.Mul);

            EmitVectorExtractF(Context, Op.Ra, 0, SizeF);

            Context.Emit(OpCodes.Sub);

            EmitScalarSetF(Context, Op.Rd, SizeF);
        }

        public static void Fnmul_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpF(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Neg);
            });
        }

        public static void Frecpe_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitUnarySoftFloatCall(Context, nameof(ASoftFloat.RecipEstimate));
            });
        }

        public static void Frecpe_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitUnarySoftFloatCall(Context, nameof(ASoftFloat.RecipEstimate));
            });
        }

        public static void Frecps_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                int SizeF = Op.Size & 1;

                if (SizeF == 0)
                {
                    Type[] TypesSsv    = new Type[] { typeof(float) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdc_R4(2f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetScalarVector128), TypesSsv));

                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SubtractScalar), TypesMulSub));

                    Context.EmitStvec(Op.Rd);

                    EmitVectorZero32_128(Context, Op.Rd);
                }
                else /* if (SizeF == 1) */
                {
                    Type[] TypesSsv    = new Type[] { typeof(double) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    Context.EmitLdc_R8(2d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetScalarVector128), TypesSsv));

                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SubtractScalar), TypesMulSub));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);

                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPRecipStepFused));
                });
            }
        }

        public static void Frecps_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                int SizeF = Op.Size & 1;

                if (SizeF == 0)
                {
                    Type[] TypesSav    = new Type[] { typeof(float) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdc_R4(2f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetAllVector128), TypesSav));

                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.Multiply), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.Subtract), TypesMulSub));

                    Context.EmitStvec(Op.Rd);

                    if (Op.RegisterSize == ARegisterSize.SIMD64)
                    {
                        EmitVectorZeroUpper(Context, Op.Rd);
                    }
                }
                else /* if (SizeF == 1) */
                {
                    Type[] TypesSav    = new Type[] { typeof(double) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    Context.EmitLdc_R8(2d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetAllVector128), TypesSav));

                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Multiply), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesMulSub));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPRecipStepFused));
                });
            }
        }

        public static void Frecpx_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPRecpX));
            });
        }

        public static void Frinta_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitVectorExtractF(Context, Op.Rn, 0, Op.Size);

            EmitRoundMathCall(Context, MidpointRounding.AwayFromZero);

            EmitScalarSetF(Context, Op.Rd, Op.Size);
        }

        public static void Frinta_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitRoundMathCall(Context, MidpointRounding.AwayFromZero);
            });
        }

        public static void Frinti_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitScalarUnaryOpF(Context, () =>
            {
                Context.EmitLdarg(ATranslatedSub.StateArgIdx);

                if (Op.Size == 0)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.RoundF));
                }
                else if (Op.Size == 1)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.Round));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            });
        }

        public static void Frinti_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int SizeF = Op.Size & 1;

            EmitVectorUnaryOpF(Context, () =>
            {
                Context.EmitLdarg(ATranslatedSub.StateArgIdx);

                if (SizeF == 0)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.RoundF));
                }
                else if (SizeF == 1)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.Round));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            });
        }

        public static void Frintm_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Floor));
            });
        }

        public static void Frintm_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Floor));
            });
        }

        public static void Frintn_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitVectorExtractF(Context, Op.Rn, 0, Op.Size);

            EmitRoundMathCall(Context, MidpointRounding.ToEven);

            EmitScalarSetF(Context, Op.Rd, Op.Size);
        }

        public static void Frintn_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitRoundMathCall(Context, MidpointRounding.ToEven);
            });
        }

        public static void Frintp_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Ceiling));
            });
        }

        public static void Frintp_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitUnaryMathCall(Context, nameof(Math.Ceiling));
            });
        }

        public static void Frintx_S(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitScalarUnaryOpF(Context, () =>
            {
                Context.EmitLdarg(ATranslatedSub.StateArgIdx);

                if (Op.Size == 0)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.RoundF));
                }
                else if (Op.Size == 1)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.Round));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            });
        }

        public static void Frintx_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            EmitVectorUnaryOpF(Context, () =>
            {
                Context.EmitLdarg(ATranslatedSub.StateArgIdx);

                if (Op.Size == 0)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.RoundF));
                }
                else if (Op.Size == 1)
                {
                    AVectorHelper.EmitCall(Context, nameof(AVectorHelper.Round));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            });
        }

        public static void Frsqrte_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpF(Context, () =>
            {
                EmitUnarySoftFloatCall(Context, nameof(ASoftFloat.InvSqrtEstimate));
            });
        }

        public static void Frsqrte_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpF(Context, () =>
            {
                EmitUnarySoftFloatCall(Context, nameof(ASoftFloat.InvSqrtEstimate));
            });
        }

        public static void Frsqrts_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                int SizeF = Op.Size & 1;

                if (SizeF == 0)
                {
                    Type[] TypesSsv    = new Type[] { typeof(float) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdc_R4(0.5f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetScalarVector128), TypesSsv));

                    Context.EmitLdc_R4(3f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetScalarVector128), TypesSsv));

                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SubtractScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.MultiplyScalar), TypesMulSub));

                    Context.EmitStvec(Op.Rd);

                    EmitVectorZero32_128(Context, Op.Rd);
                }
                else /* if (SizeF == 1) */
                {
                    Type[] TypesSsv    = new Type[] { typeof(double) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    Context.EmitLdc_R8(0.5d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetScalarVector128), TypesSsv));

                    Context.EmitLdc_R8(3d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetScalarVector128), TypesSsv));

                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.MultiplyScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SubtractScalar), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.MultiplyScalar), TypesMulSub));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);

                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPRSqrtStepFused));
                });
            }
        }

        public static void Frsqrts_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse2)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                int SizeF = Op.Size & 1;

                if (SizeF == 0)
                {
                    Type[] TypesSav    = new Type[] { typeof(float) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<float>), typeof(Vector128<float>) };

                    Context.EmitLdc_R4(0.5f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetAllVector128), TypesSav));

                    Context.EmitLdc_R4(3f);
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.SetAllVector128), TypesSav));

                    Context.EmitLdvec(Op.Rn);
                    Context.EmitLdvec(Op.Rm);

                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.Multiply), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.Subtract), TypesMulSub));
                    Context.EmitCall(typeof(Sse).GetMethod(nameof(Sse.Multiply), TypesMulSub));

                    Context.EmitStvec(Op.Rd);

                    if (Op.RegisterSize == ARegisterSize.SIMD64)
                    {
                        EmitVectorZeroUpper(Context, Op.Rd);
                    }
                }
                else /* if (SizeF == 1) */
                {
                    Type[] TypesSav    = new Type[] { typeof(double) };
                    Type[] TypesMulSub = new Type[] { typeof(Vector128<double>), typeof(Vector128<double>) };

                    Context.EmitLdc_R8(0.5d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetAllVector128), TypesSav));

                    Context.EmitLdc_R8(3d);
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetAllVector128), TypesSav));

                    EmitLdvecWithCastToDouble(Context, Op.Rn);
                    EmitLdvecWithCastToDouble(Context, Op.Rm);

                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Multiply), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesMulSub));
                    Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Multiply), TypesMulSub));

                    EmitStvecWithCastFromDouble(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPRSqrtStepFused));
                });
            }
        }

        public static void Fsqrt_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.SqrtScalar));
            }
            else
            {
                EmitScalarUnaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPSqrt));
                });
            }
        }

        public static void Fsqrt_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Sqrt));
            }
            else
            {
                EmitVectorUnaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPSqrt));
                });
            }
        }

        public static void Fsub_S(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitScalarSseOrSse2OpF(Context, nameof(Sse.SubtractScalar));
            }
            else
            {
                EmitScalarBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPSub));
                });
            }
        }

        public static void Fsub_V(AILEmitterCtx Context)
        {
            if (AOptimizations.FastFP && AOptimizations.UseSse
                                      && AOptimizations.UseSse2)
            {
                EmitVectorSseOrSse2OpF(Context, nameof(Sse.Subtract));
            }
            else
            {
                EmitVectorBinaryOpF(Context, () =>
                {
                    EmitSoftFloatCall(Context, nameof(ASoftFloat_32.FPSub));
                });
            }
        }

        public static void Mla_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Add);
            });
        }

        public static void Mla_Ve(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpByElemZx(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Add);
            });
        }

        public static void Mls_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Sub);
            });
        }

        public static void Mls_Ve(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpByElemZx(Context, () =>
            {
                Context.Emit(OpCodes.Mul);
                Context.Emit(OpCodes.Sub);
            });
        }

        public static void Mul_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpZx(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Mul_Ve(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpByElemZx(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Neg_S(AILEmitterCtx Context)
        {
            EmitScalarUnaryOpSx(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Neg_V(AILEmitterCtx Context)
        {
            EmitVectorUnaryOpSx(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Raddhn_V(AILEmitterCtx Context)
        {
            EmitHighNarrow(Context, () => Context.Emit(OpCodes.Add), Round: true);
        }

        public static void Rsubhn_V(AILEmitterCtx Context)
        {
            EmitHighNarrow(Context, () => Context.Emit(OpCodes.Sub), Round: true);
        }

        public static void Saba_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpSx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);

                Context.Emit(OpCodes.Add);
            });
        }

        public static void Sabal_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmTernaryOpSx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);

                Context.Emit(OpCodes.Add);
            });
        }

        public static void Sabd_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpSx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);
            });
        }

        public static void Sabdl_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmBinaryOpSx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);
            });
        }

        public static void Sadalp_V(AILEmitterCtx Context)
        {
            EmitAddLongPairwise(Context, Signed: true, Accumulate: true);
        }

        public static void Saddl_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse41)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                Type[] TypesSrl = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt = new Type[] { VectorIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesAdd = new Type[] { VectorIntTypesPerSizeLog2[Op.Size + 1],
                                               VectorIntTypesPerSizeLog2[Op.Size + 1] };

                string[] NamesCvt = new string[] { nameof(Sse41.ConvertToVector128Int16),
                                                   nameof(Sse41.ConvertToVector128Int32),
                                                   nameof(Sse41.ConvertToVector128Int64) };

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAdd));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmBinaryOpSx(Context, () => Context.Emit(OpCodes.Add));
            }
        }

        public static void Saddlp_V(AILEmitterCtx Context)
        {
            EmitAddLongPairwise(Context, Signed: true, Accumulate: false);
        }

        public static void Saddw_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRmBinaryOpSx(Context, () => Context.Emit(OpCodes.Add));
        }

        public static void Shadd_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size > 0)
            {
                Type[] TypesSra       = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesAndXorAdd = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], VectorIntTypesPerSizeLog2[Op.Size] };

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);

                Context.Emit(OpCodes.Dup);
                Context.EmitStvectmp();

                EmitLdvecWithSignedCast(Context, Op.Rm, Op.Size);

                Context.Emit(OpCodes.Dup);
                Context.EmitStvectmp2();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.And), TypesAndXorAdd));

                Context.EmitLdvectmp();
                Context.EmitLdvectmp2();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Xor), TypesAndXorAdd));

                Context.EmitLdc_I4(1);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightArithmetic), TypesSra));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAndXorAdd));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpSx(Context, () =>
                {
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr);
                });
            }
        }

        public static void Shsub_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size < 2)
            {
                Type[] TypesSav    = new Type[] { IntTypesPerSizeLog2[Op.Size] };
                Type[] TypesAddSub = new Type[] { VectorIntTypesPerSizeLog2 [Op.Size], VectorIntTypesPerSizeLog2 [Op.Size] };
                Type[] TypesAvg    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], VectorUIntTypesPerSizeLog2[Op.Size] };

                Context.EmitLdc_I4(Op.Size == 0 ? sbyte.MinValue : short.MinValue);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetAllVector128), TypesSav));

                Context.EmitStvectmp();

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);
                Context.EmitLdvectmp();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAddSub));

                Context.Emit(OpCodes.Dup);

                EmitLdvecWithSignedCast(Context, Op.Rm, Op.Size);
                Context.EmitLdvectmp();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAddSub));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Average), TypesAvg));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesAddSub));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpSx(Context, () =>
                {
                    Context.Emit(OpCodes.Sub);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr);
                });
            }
        }

        public static void Smax_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(long), typeof(long) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Max), Types);

            EmitVectorBinaryOpSx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Smaxp_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(long), typeof(long) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Max), Types);

            EmitVectorPairwiseOpSx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Smin_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(long), typeof(long) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Min), Types);

            EmitVectorBinaryOpSx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Sminp_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(long), typeof(long) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Min), Types);

            EmitVectorPairwiseOpSx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Smlal_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse41 && Op.Size < 2)
            {
                Type[] TypesSrl    = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt    = new Type[] { VectorIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesMulAdd = new Type[] { VectorIntTypesPerSizeLog2[Op.Size + 1],
                                                  VectorIntTypesPerSizeLog2[Op.Size + 1] };

                Type TypeMul = Op.Size == 0 ? typeof(Sse2) : typeof(Sse41);

                string NameCvt = Op.Size == 0
                    ? nameof(Sse41.ConvertToVector128Int16)
                    : nameof(Sse41.ConvertToVector128Int32);

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithSignedCast(Context, Op.Rd, Op.Size + 1);

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                EmitLdvecWithSignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                Context.EmitCall(TypeMul.GetMethod(nameof(Sse2.MultiplyLow), TypesMulAdd));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesMulAdd));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmTernaryOpSx(Context, () =>
                {
                    Context.Emit(OpCodes.Mul);
                    Context.Emit(OpCodes.Add);
                });
            }
        }

        public static void Smlsl_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse41 && Op.Size < 2)
            {
                Type[] TypesSrl    = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt    = new Type[] { VectorIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesMulSub = new Type[] { VectorIntTypesPerSizeLog2[Op.Size + 1],
                                                  VectorIntTypesPerSizeLog2[Op.Size + 1] };

                Type TypeMul = Op.Size == 0 ? typeof(Sse2) : typeof(Sse41);

                string NameCvt = Op.Size == 0
                    ? nameof(Sse41.ConvertToVector128Int16)
                    : nameof(Sse41.ConvertToVector128Int32);

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithSignedCast(Context, Op.Rd, Op.Size + 1);

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                EmitLdvecWithSignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                Context.EmitCall(TypeMul.GetMethod(nameof(Sse2.MultiplyLow), TypesMulSub));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesMulSub));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmTernaryOpSx(Context, () =>
                {
                    Context.Emit(OpCodes.Mul);
                    Context.Emit(OpCodes.Sub);
                });
            }
        }

        public static void Smull_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmBinaryOpSx(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Sqabs_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingUnaryOpSx(Context, () => EmitAbs(Context));
        }

        public static void Sqabs_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingUnaryOpSx(Context, () => EmitAbs(Context));
        }

        public static void Sqadd_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpSx(Context, SaturatingFlags.Add);
        }

        public static void Sqadd_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpSx(Context, SaturatingFlags.Add);
        }

        public static void Sqdmulh_S(AILEmitterCtx Context)
        {
            EmitSaturatingBinaryOp(Context, () => EmitDoublingMultiplyHighHalf(Context, Round: false), SaturatingFlags.ScalarSx);
        }

        public static void Sqdmulh_V(AILEmitterCtx Context)
        {
            EmitSaturatingBinaryOp(Context, () => EmitDoublingMultiplyHighHalf(Context, Round: false), SaturatingFlags.VectorSx);
        }

        public static void Sqneg_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingUnaryOpSx(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Sqneg_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingUnaryOpSx(Context, () => Context.Emit(OpCodes.Neg));
        }

        public static void Sqrdmulh_S(AILEmitterCtx Context)
        {
            EmitSaturatingBinaryOp(Context, () => EmitDoublingMultiplyHighHalf(Context, Round: true), SaturatingFlags.ScalarSx);
        }

        public static void Sqrdmulh_V(AILEmitterCtx Context)
        {
            EmitSaturatingBinaryOp(Context, () => EmitDoublingMultiplyHighHalf(Context, Round: true), SaturatingFlags.VectorSx);
        }

        public static void Sqsub_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpSx(Context, SaturatingFlags.Sub);
        }

        public static void Sqsub_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpSx(Context, SaturatingFlags.Sub);
        }

        public static void Sqxtn_S(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.ScalarSxSx);
        }

        public static void Sqxtn_V(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.VectorSxSx);
        }

        public static void Sqxtun_S(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.ScalarSxZx);
        }

        public static void Sqxtun_V(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.VectorSxZx);
        }

        public static void Srhadd_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size < 2)
            {
                Type[] TypesSav    = new Type[] { IntTypesPerSizeLog2[Op.Size] };
                Type[] TypesSubAdd = new Type[] { VectorIntTypesPerSizeLog2 [Op.Size], VectorIntTypesPerSizeLog2 [Op.Size] };
                Type[] TypesAvg    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], VectorUIntTypesPerSizeLog2[Op.Size] };

                Context.EmitLdc_I4(Op.Size == 0 ? sbyte.MinValue : short.MinValue);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.SetAllVector128), TypesSav));

                Context.Emit(OpCodes.Dup);
                Context.EmitStvectmp();

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);
                Context.EmitLdvectmp();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesSubAdd));

                EmitLdvecWithSignedCast(Context, Op.Rm, Op.Size);
                Context.EmitLdvectmp();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesSubAdd));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Average), TypesAvg));
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add),     TypesSubAdd));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpSx(Context, () =>
                {
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr);
                });
            }
        }

        public static void Ssubl_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse41)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                Type[] TypesSrl = new Type[] { VectorIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt = new Type[] { VectorIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesSub = new Type[] { VectorIntTypesPerSizeLog2[Op.Size + 1],
                                               VectorIntTypesPerSizeLog2[Op.Size + 1] };

                string[] NamesCvt = new string[] { nameof(Sse41.ConvertToVector128Int16),
                                                   nameof(Sse41.ConvertToVector128Int32),
                                                   nameof(Sse41.ConvertToVector128Int64) };

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithSignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesSub));

                EmitStvecWithSignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmBinaryOpSx(Context, () => Context.Emit(OpCodes.Sub));
            }
        }

        public static void Ssubw_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRmBinaryOpSx(Context, () => Context.Emit(OpCodes.Sub));
        }

        public static void Sub_S(AILEmitterCtx Context)
        {
            EmitScalarBinaryOpZx(Context, () => Context.Emit(OpCodes.Sub));
        }

        public static void Sub_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse2)
            {
                EmitSse2Op(Context, nameof(Sse2.Subtract));
            }
            else
            {
                EmitVectorBinaryOpZx(Context, () => Context.Emit(OpCodes.Sub));
            }
        }

        public static void Subhn_V(AILEmitterCtx Context)
        {
            EmitHighNarrow(Context, () => Context.Emit(OpCodes.Sub), Round: false);
        }

        public static void Suqadd_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpSx(Context, SaturatingFlags.Accumulate);
        }

        public static void Suqadd_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpSx(Context, SaturatingFlags.Accumulate);
        }

        public static void Uaba_V(AILEmitterCtx Context)
        {
            EmitVectorTernaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);

                Context.Emit(OpCodes.Add);
            });
        }

        public static void Uabal_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmTernaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);

                Context.Emit(OpCodes.Add);
            });
        }

        public static void Uabd_V(AILEmitterCtx Context)
        {
            EmitVectorBinaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);
            });
        }

        public static void Uabdl_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmBinaryOpZx(Context, () =>
            {
                Context.Emit(OpCodes.Sub);
                EmitAbs(Context);
            });
        }

        public static void Uadalp_V(AILEmitterCtx Context)
        {
            EmitAddLongPairwise(Context, Signed: false, Accumulate: true);
        }

        public static void Uaddl_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse41)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                Type[] TypesSrl = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesAdd = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size + 1],
                                               VectorUIntTypesPerSizeLog2[Op.Size + 1] };

                string[] NamesCvt = new string[] { nameof(Sse41.ConvertToVector128Int16),
                                                   nameof(Sse41.ConvertToVector128Int32),
                                                   nameof(Sse41.ConvertToVector128Int64) };

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAdd));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmBinaryOpZx(Context, () => Context.Emit(OpCodes.Add));
            }
        }

        public static void Uaddlp_V(AILEmitterCtx Context)
        {
            EmitAddLongPairwise(Context, Signed: false, Accumulate: false);
        }

        public static void Uaddlv_V(AILEmitterCtx Context)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Bytes = Op.GetBitsCount() >> 3;
            int Elems = Bytes >> Op.Size;

            EmitVectorExtractZx(Context, Op.Rn, 0, Op.Size);

            for (int Index = 1; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size);

                Context.Emit(OpCodes.Add);
            }

            EmitScalarSet(Context, Op.Rd, Op.Size + 1);
        }

        public static void Uaddw_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRmBinaryOpZx(Context, () => Context.Emit(OpCodes.Add));
        }

        public static void Uhadd_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size > 0)
            {
                Type[] TypesSrl       = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesAndXorAdd = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], VectorUIntTypesPerSizeLog2[Op.Size] };

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);

                Context.Emit(OpCodes.Dup);
                Context.EmitStvectmp();

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.Emit(OpCodes.Dup);
                Context.EmitStvectmp2();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.And), TypesAndXorAdd));

                Context.EmitLdvectmp();
                Context.EmitLdvectmp2();

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Xor), TypesAndXorAdd));

                Context.EmitLdc_I4(1);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical), TypesSrl));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesAndXorAdd));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpZx(Context, () =>
                {
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr_Un);
                });
            }
        }

        public static void Uhsub_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size < 2)
            {
                Type[] TypesAvgSub = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], VectorUIntTypesPerSizeLog2[Op.Size] };

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);
                Context.Emit(OpCodes.Dup);

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Average), TypesAvgSub));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesAvgSub));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpZx(Context, () =>
                {
                    Context.Emit(OpCodes.Sub);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr_Un);
                });
            }
        }

        public static void Umax_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(ulong), typeof(ulong) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Max), Types);

            EmitVectorBinaryOpZx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Umaxp_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(ulong), typeof(ulong) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Max), Types);

            EmitVectorPairwiseOpZx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Umin_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(ulong), typeof(ulong) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Min), Types);

            EmitVectorBinaryOpZx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Uminp_V(AILEmitterCtx Context)
        {
            Type[] Types = new Type[] { typeof(ulong), typeof(ulong) };

            MethodInfo MthdInfo = typeof(Math).GetMethod(nameof(Math.Min), Types);

            EmitVectorPairwiseOpZx(Context, () => Context.EmitCall(MthdInfo));
        }

        public static void Umlal_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse41 && Op.Size < 2)
            {
                Type[] TypesSrl    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesMulAdd = new Type[] { VectorIntTypesPerSizeLog2 [Op.Size + 1],
                                                  VectorIntTypesPerSizeLog2 [Op.Size + 1] };

                Type TypeMul = Op.Size == 0 ? typeof(Sse2) : typeof(Sse41);

                string NameCvt = Op.Size == 0
                    ? nameof(Sse41.ConvertToVector128Int16)
                    : nameof(Sse41.ConvertToVector128Int32);

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                Context.EmitCall(TypeMul.GetMethod(nameof(Sse2.MultiplyLow), TypesMulAdd));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Add), TypesMulAdd));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmTernaryOpZx(Context, () =>
                {
                    Context.Emit(OpCodes.Mul);
                    Context.Emit(OpCodes.Add);
                });
            }
        }

        public static void Umlsl_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse41 && Op.Size < 2)
            {
                Type[] TypesSrl    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt    = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesMulSub = new Type[] { VectorIntTypesPerSizeLog2 [Op.Size + 1],
                                                  VectorIntTypesPerSizeLog2 [Op.Size + 1] };

                Type TypeMul = Op.Size == 0 ? typeof(Sse2) : typeof(Sse41);

                string NameCvt = Op.Size == 0
                    ? nameof(Sse41.ConvertToVector128Int16)
                    : nameof(Sse41.ConvertToVector128Int32);

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NameCvt, TypesCvt));

                Context.EmitCall(TypeMul.GetMethod(nameof(Sse2.MultiplyLow), TypesMulSub));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesMulSub));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmTernaryOpZx(Context, () =>
                {
                    Context.Emit(OpCodes.Mul);
                    Context.Emit(OpCodes.Sub);
                });
            }
        }

        public static void Umull_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRnRmBinaryOpZx(Context, () => Context.Emit(OpCodes.Mul));
        }

        public static void Uqadd_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpZx(Context, SaturatingFlags.Add);
        }

        public static void Uqadd_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpZx(Context, SaturatingFlags.Add);
        }

        public static void Uqsub_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpZx(Context, SaturatingFlags.Sub);
        }

        public static void Uqsub_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpZx(Context, SaturatingFlags.Sub);
        }

        public static void Uqxtn_S(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.ScalarZxZx);
        }

        public static void Uqxtn_V(AILEmitterCtx Context)
        {
            EmitSaturatingNarrowOp(Context, SaturatingNarrowFlags.VectorZxZx);
        }

        public static void Urhadd_V(AILEmitterCtx Context)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            if (AOptimizations.UseSse2 && Op.Size < 2)
            {
                Type[] TypesAvg = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], VectorUIntTypesPerSizeLog2[Op.Size] };

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);
                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Average), TypesAvg));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size);

                if (Op.RegisterSize == ARegisterSize.SIMD64)
                {
                    EmitVectorZeroUpper(Context, Op.Rd);
                }
            }
            else
            {
                EmitVectorBinaryOpZx(Context, () =>
                {
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Add);

                    Context.Emit(OpCodes.Ldc_I4_1);
                    Context.Emit(OpCodes.Shr_Un);
                });
            }
        }

        public static void Usqadd_S(AILEmitterCtx Context)
        {
            EmitScalarSaturatingBinaryOpZx(Context, SaturatingFlags.Accumulate);
        }

        public static void Usqadd_V(AILEmitterCtx Context)
        {
            EmitVectorSaturatingBinaryOpZx(Context, SaturatingFlags.Accumulate);
        }

        public static void Usubl_V(AILEmitterCtx Context)
        {
            if (AOptimizations.UseSse41)
            {
                AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

                Type[] TypesSrl = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size], typeof(byte) };
                Type[] TypesCvt = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size] };
                Type[] TypesSub = new Type[] { VectorUIntTypesPerSizeLog2[Op.Size + 1],
                                               VectorUIntTypesPerSizeLog2[Op.Size + 1] };

                string[] NamesCvt = new string[] { nameof(Sse41.ConvertToVector128Int16),
                                                   nameof(Sse41.ConvertToVector128Int32),
                                                   nameof(Sse41.ConvertToVector128Int64) };

                int NumBytes = Op.RegisterSize == ARegisterSize.SIMD128 ? 8 : 0;

                EmitLdvecWithUnsignedCast(Context, Op.Rn, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                EmitLdvecWithUnsignedCast(Context, Op.Rm, Op.Size);

                Context.EmitLdc_I4(NumBytes);
                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.ShiftRightLogical128BitLane), TypesSrl));

                Context.EmitCall(typeof(Sse41).GetMethod(NamesCvt[Op.Size], TypesCvt));

                Context.EmitCall(typeof(Sse2).GetMethod(nameof(Sse2.Subtract), TypesSub));

                EmitStvecWithUnsignedCast(Context, Op.Rd, Op.Size + 1);
            }
            else
            {
                EmitVectorWidenRnRmBinaryOpZx(Context, () => Context.Emit(OpCodes.Sub));
            }
        }

        public static void Usubw_V(AILEmitterCtx Context)
        {
            EmitVectorWidenRmBinaryOpZx(Context, () => Context.Emit(OpCodes.Sub));
        }

        private static void EmitAbs(AILEmitterCtx Context)
        {
            AILLabel LblTrue = new AILLabel();

            Context.Emit(OpCodes.Dup);
            Context.Emit(OpCodes.Ldc_I4_0);
            Context.Emit(OpCodes.Bge_S, LblTrue);

            Context.Emit(OpCodes.Neg);

            Context.MarkLabel(LblTrue);
        }

        private static void EmitAddLongPairwise(AILEmitterCtx Context, bool Signed, bool Accumulate)
        {
            AOpCodeSimd Op = (AOpCodeSimd)Context.CurrOp;

            int Words = Op.GetBitsCount() >> 4;
            int Pairs = Words >> Op.Size;

            for (int Index = 0; Index < Pairs; Index++)
            {
                int Idx = Index << 1;

                EmitVectorExtract(Context, Op.Rn, Idx,     Op.Size, Signed);
                EmitVectorExtract(Context, Op.Rn, Idx + 1, Op.Size, Signed);

                Context.Emit(OpCodes.Add);

                if (Accumulate)
                {
                    EmitVectorExtract(Context, Op.Rd, Index, Op.Size + 1, Signed);

                    Context.Emit(OpCodes.Add);
                }

                EmitVectorInsertTmp(Context, Index, Op.Size + 1);
            }

            Context.EmitLdvectmp();
            Context.EmitStvec(Op.Rd);

            if (Op.RegisterSize == ARegisterSize.SIMD64)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }

        private static void EmitDoublingMultiplyHighHalf(AILEmitterCtx Context, bool Round)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int ESize = 8 << Op.Size;

            Context.Emit(OpCodes.Mul);

            if (!Round)
            {
                Context.EmitAsr(ESize - 1);
            }
            else
            {
                long RoundConst = 1L << (ESize - 1);

                AILLabel LblTrue = new AILLabel();

                Context.EmitLsl(1);

                Context.EmitLdc_I8(RoundConst);

                Context.Emit(OpCodes.Add);

                Context.EmitAsr(ESize);

                Context.Emit(OpCodes.Dup);
                Context.EmitLdc_I8((long)int.MinValue);
                Context.Emit(OpCodes.Bne_Un_S, LblTrue);

                Context.Emit(OpCodes.Neg);

                Context.MarkLabel(LblTrue);
            }
        }

        private static void EmitHighNarrow(AILEmitterCtx Context, Action Emit, bool Round)
        {
            AOpCodeSimdReg Op = (AOpCodeSimdReg)Context.CurrOp;

            int Elems = 8 >> Op.Size;

            int ESize = 8 << Op.Size;

            int Part = Op.RegisterSize == ARegisterSize.SIMD128 ? Elems : 0;

            long RoundConst = 1L << (ESize - 1);

            if (Part != 0)
            {
                Context.EmitLdvec(Op.Rd);
                Context.EmitStvectmp();
            }

            for (int Index = 0; Index < Elems; Index++)
            {
                EmitVectorExtractZx(Context, Op.Rn, Index, Op.Size + 1);
                EmitVectorExtractZx(Context, Op.Rm, Index, Op.Size + 1);

                Emit();

                if (Round)
                {
                    Context.EmitLdc_I8(RoundConst);

                    Context.Emit(OpCodes.Add);
                }

                Context.EmitLsr(ESize);

                EmitVectorInsertTmp(Context, Part + Index, Op.Size);
            }

            Context.EmitLdvectmp();
            Context.EmitStvec(Op.Rd);

            if (Part == 0)
            {
                EmitVectorZeroUpper(Context, Op.Rd);
            }
        }
    }
}
