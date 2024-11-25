﻿using DirectoryManager.Utilities.Validation;
using ExCSS;

namespace DirectoryManager.Utilities.Tests.Validation
{
    public class CssValidatorTests
    {
        [Fact]
        public void ValidateCss_ValidHoverCss_ShouldReturnTrue()
        {
            // Arrange
            var validCss = @"
<style>


a:hover {
    color: #66aaff; /* Slightly lighter blue on hover */
    text-decoration: none;
}

h2 {
color: white;
    text-shadow: 2px 2px 4px rgb(94 94 94 / 80%);
}

</style>

            ";

            // Act
            var result = CssValidator.IsCssValid(validCss);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_ValidCss_ShouldReturnTrue()
        {
            // Arrange
            var validCss = @"
                body {
                    background-color: #000000;
                    color: white;
                    margin: 0;
                    padding: 0;
                }

                h1 {
                    font-size: 24px;
                    font-weight: bold;
                }
            ";

            // Act
            var result = CssValidator.IsCssValid(validCss);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_InvalidCss_ShouldReturnFalse()
        {
            // Arrange
            var invalidCss = @"
                body {
                    background-color: #000000;
                    color: white;
                    margin 0; /* Missing colon */
                    padding: 0;
                }

                h1 {
                    font-size: 24px
                    font-weight: bold;
                }
            ";

            // Act
            var result = CssValidator.IsCssValid(invalidCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_EmptyCss_ShouldReturnFalse()
        {
            // Arrange
            var emptyCss = "";

            // Act
            var result = CssValidator.IsCssValid(emptyCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_ValidCssWithExtraSpaces_ShouldReturnTrue()
        {
            // Arrange
            var validCssWithSpaces = @"
                .container   {
                    padding   : 10px;
                    margin : 0  ;
                }
            ";

            // Act
            var result = CssValidator.IsCssValid(validCssWithSpaces);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_MalformedCss_ShouldReturnFalse()
        {
            // Arrange
            var malformedCss = @"
                .button {
                    background-color: #FFF;
                    font-size: 14px;
                /* Missing closing brace */
            ";

            // Act
            var result = CssValidator.IsCssValid(malformedCss);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_CssWithoutSemicolon_ShouldReturnFalse()
        {
            // Arrange
            var cssWithoutSemicolon = @"
                body {
                    background-color: #000000
                    color: white;
                }
            ";

            // Act
            var result = CssValidator.IsCssValid(cssWithoutSemicolon);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateCss_ValidSingleLineCss_ShouldReturnTrue()
        {
            // Arrange
            var singleLineCss = "body { margin: 0; padding: 0; }";

            // Act
            var result = CssValidator.IsCssValid(singleLineCss);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateCss_LargeCssInput_ShouldReturnTrue()
        {
            // Arrange
            var cssInput = @"

<style>
/* General Styles */
body {
    background-color: #000000eb;
    color: #FFFFFF;
    font-family: Helvetica;
    margin: 0 auto;
    max-width: 62rem;
    padding: 0;
    overflow-x: hidden; /* Prevent horizontal scrolling */
    box-sizing: border-box; /* Ensure padding and borders don't add to element size */
}

h1, h3, h4 {
    margin: 0;
    padding: 0;
}

h1 {
    margin-bottom: 5px;
}

h1.home, h4.home { text-align: center;}

h3 {
    margin-bottom: 0;
    padding-bottom: 0;
}

a {
    color: #008cff;
}

a:hover {
    text-decoration: none;
}

a.btn {
    text-decoration: none;
}

li p {
    margin: 0;
    margin-left: -20px;
}

.table td {
    padding: 5px;
}


table tbody tr:nth-child(even) {
    background-color:#262626;
    color: white;
}


.input-width { width: 330px; }

.right {
    text-align: right;
}

/* Specific Sections */
.directory-entry-details {
    margin-bottom: 150px;
}

.create-sponsored-listing {
    text-align: right;
    font-size: 13px;
    font-style: italic;
    padding: 1px;
    margin-top: 4px;
    margin-bottom: -8px;
}

.sponsored-section {
    background-color: #1E1E1E;
    border: 5px solid #FF6600;
    padding: 3px;
    font-size: 18px;
    border-radius: 10px;
    box-shadow:  0 2px 4px rgb(195 195 195 / 50%);
}

.sponsored-section p {
    margin: 0;
    padding-bottom: 0;
}

.sponsored-section ul {
    list-style: none;
    margin: 2px 0 4px;
    padding-left: 0;
}

.sponsored-section li {
    margin-bottom: 9px;
}

.sponsored-section > p:nth-child(1) > b:nth-child(1) {
    font-size: 14px;
    font-style: italic;
    color: #727272;
}

li.sponsored {
    background-color: #1E1E1E;
    border: 2px solid #FF6600;
    padding: 3px;
    margin: 3px 0;
    font-size: 18px;
    border-radius: 10px;
    margin-left: -25px;
}

li.sponsored p { margin-left:0px; }

.sub-category-sponsored-listing {
    font-style: italic;
    font-size: 12px;
    padding-top: 0;
    margin-top: auto;
}

.legend p {color: #fff;margin-bottom: 3px; margin-top:3px; font-size: 14px;}

p.last_update { margin: 0; text-align: center; }

html body div#main-content ul.newest_items li ul li p.small-font { margin-left: 0 !important; }

.adspace-full p{
    font-size: 18px;
    color: #FF6600;
    margin: 3px 0;
}

/* Form Styling */
.form-group {
    margin-bottom: 20px;
}

.form-group label {
    display: block;
}

.text-danger, .alert-danger {
    color: red;
}

.btn {
    background-color: green;
    border: none;
    padding: 10px 20px;
    border-radius: 4px;
    color: white;
    font-weight: bold;
}

/* Breadcrumb */
.breadcrumb {
    display: flex;
    flex-wrap: wrap;
    list-style: none;
    padding-left: 0;
}

.breadcrumb-item {
    display: inline-block;
    font-size: 1rem;
}

.breadcrumb-item + .breadcrumb-item::before {
    content: "">"";
    padding: 2px 4px;
}

.breadcrumb-item.active {
    color: #6c757d;
}

.breadcrumb-item a {
    text-decoration: none;
}

.breadcrumb-item a:hover {
    text-decoration: underline;
}

/* Top Banner */
div#banner {
    text-align: center;
    position: absolute;
    top: 0;
    left: 0;
    color: #FFF;
    font-weight: bold;
    background-color: #FF6600;
    width: 100%;
    padding: 5px 0;
    font-size: 14px;
    box-sizing: border-box;
}

div#banner-content {
    margin: 0 auto;
    max-width: 1000px;
    padding: 0 10px;
}

div#main-content {
    padding-top: 50px;
    margin-left: 10px;
    margin-right: 10px;
}

#donate p { font-size: 14px;}

/* Image and Text Block Layout */
.top-container {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    max-width: 100%;
}

.top-container img {
    width: 333px;
    height: auto;
    max-width: 100%;
}

.text-block {
    margin-left: 20px;
    flex: 1;
    max-width: 100%;
}

.top-container p {
    text-align: justify;
    font-size: 14px;
}

/* Responsive Styling for Mobile */
@media (max-width: 768px) {
    div#banner {
        padding: 8px 5px;
        font-size: 14px;
    }

    div#main-content {
        padding-top: 70px;
    }

    div#banner-content {
        font-size: 14px;
    }

    .top-container {
        flex-direction: column;
    }

    .text-block {
        margin-left: 0;
        padding: 0 10px;
    }

    .top-container img {
        width: 100%;
        max-width: 333px;
    }
}

/* Miscellaneous */
.hidden {
    display: none;
}

:checked + .hidden {
    display: block;
}

ul#categories_list {
    list-style-type: none;
    margin: 0;
    padding: 0;
}

ul#categories_list li label {
    font-size: 20px;
    font-weight: bold;
    padding-right: 15px;
}

ul#categories_list li input[type=checkbox] {
    display: none;
}

footer {
    margin-top: 30px;
    padding: 10px;
    border-top: 1px solid #FFFFFF;
}

.small-font {
    font-size: 12px;
    font-family: 'Lucida Console', 'Lucida Sans', 'Lucida Grande', 'Geneva', Verdana, sans-serif;
}

/* Toggle Image Visibility */
#toggleImageCheckbox {
    display: none;
}

#toggleImage {
    display: none;
}

#toggleImageCheckbox:checked ~ #toggleImage {
    display: block;
}

.multi-line-text {
    word-break: break-word;
    overflow-wrap: anywhere;
    white-space: normal;
    display: inline;
}



ul.blank_list_item li {
     list-style-type: none;
}


label.expansion_item {
     cursor:pointer;
     padding-right: 5px;
}    </style>  ";

            // Act
            var result = CssValidator.IsCssValid(cssInput);

            // Assert
            Assert.True(result);
        }
    }
}
