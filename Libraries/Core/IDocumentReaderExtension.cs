﻿/* ------------------------------------------------------------------------- */
//
// Copyright (c) 2010 CubeSoft, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
/* ------------------------------------------------------------------------- */
using System.Collections.Generic;
using System.Linq;

namespace Cube.Pdf.Mixin
{
    /* --------------------------------------------------------------------- */
    ///
    /// IDocumentReaderExtension
    ///
    /// <summary>
    /// IDocumentReader の拡張用クラスです。
    /// </summary>
    ///
    /* --------------------------------------------------------------------- */
    public static class IDocumentReaderExtension
    {
        #region Methods

        /* ----------------------------------------------------------------- */
        ///
        /// GetPage
        ///
        /// <summary>
        /// 指定されたページ番号に対応するページ情報を取得します。
        /// </summary>
        ///
        /// <param name="src">IDocumentReader オブジェクト</param>
        /// <param name="pagenum">1 から始まるページ番号</param>
        ///
        /// <returns>Page オブジェクト</returns>
        ///
        /// <remarks>
        /// IDocumentReader.Pages が IList(Page) または IReadOnlyList(Page)
        /// を実装している場合は O(1) で Page オブジェクトを取得します。
        /// それ以外の場合は O(n) の時間を要します。
        /// </remarks>
        ///
        /* ----------------------------------------------------------------- */
        public static Page GetPage(this IDocumentReader src, int pagenum)
        {
            var index = pagenum - 1;
            if (src.Pages is IReadOnlyList<Page> l0) return l0[index];
            if (src.Pages is IList<Page> l1) return l1[index];
            return src.Pages.Skip(index).First();
        }

        #endregion
    }
}
