module.exports = {
  default: {
    paths: ['../specs/features/*.feature'],
    require: ['features/step-definitions/**/*.ts', 'features/support/**/*.ts'],
    requireModule: ['ts-node/register'],
    format: ['progress', 'html:test-results/cucumber-report.html'],
    publishQuiet: true,
  },
};
